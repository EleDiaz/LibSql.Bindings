// This rust lib was extracted from the https://github.com/tursodatabase/libsql and their 
// c-bindings, and modified in many aspects to be use in LibSql.Bindings

#![allow(clippy::missing_safety_doc)]
#![allow(non_camel_case_types)]
#[macro_use]
extern crate lazy_static;

mod types;

use std::{ops::Deref, ptr::null};

use crate::types::LibSqlConfig;
use libsql::{errors, LoadExtensionGuard};
use tokio::runtime::Runtime;
use types::{
    blob, replicated, LIBSQL_TRANSACTION_DEFERRED, LIBSQL_TRANSACTION_EXCLUSIVE,
    LIBSQL_TRANSACTION_IMMEDIATE, LIBSQL_TRANSACTION_READONLY,
};

lazy_static! {
    static ref RT: Runtime = tokio::runtime::Runtime::new().unwrap();
}

fn translate_string(s: String) -> *const std::ffi::c_char {
    match std::ffi::CString::new(s) {
        Ok(s) => s.into_raw(),
        Err(_) => std::ptr::null(),
    }
}

unsafe fn set_err_msg(msg: String, output: *mut *const std::ffi::c_char) {
    if !output.is_null() {
        *output = translate_string(msg);
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_enable_internal_tracing() -> std::ffi::c_int {
    if tracing_subscriber::fmt::try_init().is_ok() {
        1
    } else {
        0
    }
}

pub unsafe fn get_ref<'a, T>(ptr: *const T) -> &'a T {
    return &(*ptr);
}

pub unsafe fn get_mut_ref<'a, T>(ptr: *mut T) -> &'a mut T {
    return &mut (*ptr);
}

////////////////////////////////////
///////////// DATABASE /////////////

#[no_mangle]
pub unsafe extern "C" fn libsql_sync(
    db: *const libsql::Database,
    out_replicated: *mut replicated,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!db.is_null());

    let db = get_ref(db);
    match RT.block_on(db.sync()) {
        Ok(replicated) => {
            if !out_replicated.is_null() {
                (*out_replicated).frame_no = replicated.frame_no().unwrap_or(0) as i32;
                (*out_replicated).frames_synced = replicated.frames_synced() as i32;
            }
            0
        }
        Err(e) => {
            set_err_msg(format!("Error syncing database: {e}"), out_err_msg);
            1
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_open_sync_with_config(
    config: LibSqlConfig,
    out_db: *mut *const libsql::Database,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    let db_path = unsafe { std::ffi::CStr::from_ptr(config.db_path) };
    let db_path = match db_path.to_str() {
        Ok(url) => url,
        Err(e) => {
            set_err_msg(format!("Wrong URL: {e}"), out_err_msg);
            return 1;
        }
    };
    let primary_url = unsafe { std::ffi::CStr::from_ptr(config.primary_url) };
    let primary_url = match primary_url.to_str() {
        Ok(url) => url,
        Err(e) => {
            set_err_msg(format!("Wrong URL: {e}"), out_err_msg);
            return 2;
        }
    };
    let auth_token = unsafe { std::ffi::CStr::from_ptr(config.auth_token) };
    let auth_token = match auth_token.to_str() {
        Ok(token) => token,
        Err(e) => {
            set_err_msg(format!("Wrong Auth Token: {e}"), out_err_msg);
            return 3;
        }
    };
    let mut builder = libsql::Builder::new_remote_replica(
        db_path,
        primary_url.to_string(),
        auth_token.to_string(),
    );
    if config.with_webpki != 0 {
        let https = hyper_rustls::HttpsConnectorBuilder::new()
            .with_webpki_roots()
            .https_or_http()
            .enable_http1()
            .build();
        builder = builder.connector(https);
    }
    if config.sync_interval > 0 {
        let interval = match config.sync_interval.try_into() {
            Ok(d) => d,
            Err(e) => {
                set_err_msg(format!("Wrong periodic sync interval: {e}"), out_err_msg);
                return 4;
            }
        };
        builder = builder.sync_interval(std::time::Duration::from_secs(interval));
    }
    builder = builder.read_your_writes(config.read_your_writes != 0);
    if !config.encryption_key.is_null() {
        let key = unsafe { std::ffi::CStr::from_ptr(config.encryption_key) };
        let key = match key.to_str() {
            Ok(k) => k,
            Err(e) => {
                set_err_msg(format!("Wrong encryption key: {e}"), out_err_msg);
                return 5;
            }
        };
        let key = bytes::Bytes::copy_from_slice(key.as_bytes());
        let config = libsql::EncryptionConfig::new(libsql::Cipher::Aes256Cbc, key);
        builder = builder.encryption_config(config)
    };
    match RT.block_on(builder.build()) {
        Ok(db) => {
            let db = Box::leak(Box::new(db));
            *out_db = db as *const libsql::Database;
            0
        }
        Err(e) => {
            set_err_msg(
                format!("Error opening db path {db_path}, primary url {primary_url}: {e}"),
                out_err_msg,
            );
            6
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_open_file(
    url: *const std::ffi::c_char,
    out_db: *mut *const libsql::Database,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    let url = unsafe { std::ffi::CStr::from_ptr(url) };
    let url = match url.to_str() {
        Ok(url) => url,
        Err(e) => {
            set_err_msg(format!("Wrong URL: {e}"), out_err_msg);
            return 1;
        }
    };
    match RT.block_on(libsql::Builder::new_local(url).build()) {
        Ok(db) => {
            let db = Box::leak(Box::new(db));
            *out_db = db as *const libsql::Database;
            0
        }
        Err(e) => {
            set_err_msg(format!("Error opening URL {url}: {e}"), out_err_msg);
            1
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_open_remote_internal(
    url: *const std::ffi::c_char,
    auth_token: *const std::ffi::c_char,
    with_webpki: bool,
    out_db: *mut *const libsql::Database,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    let url = unsafe { std::ffi::CStr::from_ptr(url) };
    let url = match url.to_str() {
        Ok(url) => url,
        Err(e) => {
            set_err_msg(format!("Wrong URL: {e}"), out_err_msg);
            return 1;
        }
    };
    let auth_token = unsafe { std::ffi::CStr::from_ptr(auth_token) };
    let auth_token = match auth_token.to_str() {
        Ok(token) => token,
        Err(e) => {
            set_err_msg(format!("Wrong Auth Token: {e}"), out_err_msg);
            return 2;
        }
    };
    let mut builder = libsql::Builder::new_remote(url.to_string(), auth_token.to_string());
    if with_webpki {
        let https = hyper_rustls::HttpsConnectorBuilder::new()
            .with_webpki_roots()
            .https_or_http()
            .enable_http1()
            .build();
        builder = builder.connector(https);
    }
    match RT.block_on(builder.build()) {
        Ok(db) => {
            let db = Box::leak(Box::new(db));
            *out_db = db as *const libsql::Database;
            0
        }
        Err(e) => {
            set_err_msg(format!("Error opening URL {url}: {e}"), out_err_msg);
            1
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_close(db: *mut libsql::Database) {
    if db.is_null() {
        return;
    }
    let _db = Box::from_raw(db);
    // TODO: change this to free LibSqlDatabase (close action would be related with assuring
    // closing current connections)
}

#[no_mangle]
pub unsafe extern "C" fn libsql_connect(
    db: *const libsql::Database,
    out_conn: *mut *const libsql::Connection,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!db.is_null());

    let db = get_ref(db);
    let conn = match db.connect() {
        Ok(conn) => conn,
        Err(err) => {
            set_err_msg(format!("Unable to connect: {}", err), out_err_msg);
            return 1;
        }
    };
    *out_conn = Box::leak(Box::new(conn)) as *const libsql::Connection;
    0
}

////////////////////////////////////
///////////// CONNECTION ///////////

#[no_mangle]
pub unsafe extern "C" fn libsql_load_extension(
    conn: *const libsql::Connection,
    path: *const std::ffi::c_char,
    entry_point: *const std::ffi::c_char,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!conn.is_null());
    debug_assert!(!path.is_null());

    let path = unsafe { std::ffi::CStr::from_ptr(path) };
    let path = match path.to_str() {
        Ok(path) => path,
        Err(e) => {
            set_err_msg(format!("Wrong path: {}", e), out_err_msg);
            return 2;
        }
    };
    let mut entry_point_option = None;
    if !entry_point.is_null() {
        let entry_point = unsafe { std::ffi::CStr::from_ptr(entry_point) };
        entry_point_option = match entry_point.to_str() {
            Ok(entry_point) => Some(entry_point),
            Err(e) => {
                set_err_msg(format!("Wrong entry point: {}", e), out_err_msg);
                return 4;
            }
        };
    }
    let conn = get_ref(conn);
    match RT.block_on(async move {
        let _guard = LoadExtensionGuard::new(conn)?;
        conn.load_extension(path, entry_point_option)?;
        Ok::<(), errors::Error>(())
    }) {
        Ok(()) => {}
        Err(e) => {
            set_err_msg(format!("Error loading extension: {}", e), out_err_msg);
            return 6;
        }
    };
    0
}

#[no_mangle]
pub unsafe extern "C" fn libsql_transaction_with_behavior(
    conn: *const libsql::Connection,
    out_transaction: *mut *const libsql::Transaction,
    transaction_behavior: std::ffi::c_int,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!conn.is_null());

    let transaction_behavior = match transaction_behavior as i8 {
        LIBSQL_TRANSACTION_DEFERRED => libsql::TransactionBehavior::Deferred,
        LIBSQL_TRANSACTION_IMMEDIATE => libsql::TransactionBehavior::Immediate,
        LIBSQL_TRANSACTION_EXCLUSIVE => libsql::TransactionBehavior::Exclusive,
        LIBSQL_TRANSACTION_READONLY => libsql::TransactionBehavior::ReadOnly,
        _ => libsql::TransactionBehavior::Deferred,
    };

    let conn = get_ref(conn);
    match RT.block_on(conn.transaction_with_behavior(transaction_behavior)) {
        Ok(transaction) => {
            let transaction = Box::leak(Box::from(transaction));
            *out_transaction = transaction;
            return 0;
        }
        Err(e) => {
            set_err_msg(format!("Error creating transaction: {e}"), out_err_msg);
            return 1;
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_query(
    conn: *const libsql::Connection,
    sql: *const std::ffi::c_char,
    out_rows: *mut *const libsql::Rows,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!conn.is_null());
    debug_assert!(!sql.is_null());

    let sql = unsafe { std::ffi::CStr::from_ptr(sql) };
    let sql = match sql.to_str() {
        Ok(sql) => sql,
        Err(e) => {
            set_err_msg(format!("Wrong SQL: {}", e), out_err_msg);
            return 1;
        }
    };
    let conn = get_ref(conn);
    match RT.block_on(conn.query(sql, ())) {
        Ok(rows_) => {
            *out_rows = Box::leak(Box::new(rows_)) as *const libsql::Rows;
            return 0;
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            return 1;
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_query_positional(
    conn: *const libsql::Connection,
    sql: *const std::ffi::c_char,
    in_positional_values: *const Vec<libsql::Value>,
    out_rows: *mut *const libsql::Rows,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!conn.is_null());
    debug_assert!(!sql.is_null());
    debug_assert!(!in_positional_values.is_null());

    let sql = unsafe { std::ffi::CStr::from_ptr(sql) };
    let sql = match sql.to_str() {
        Ok(sql) => sql,
        Err(e) => {
            set_err_msg(format!("Wrong SQL: {}", e), out_err_msg);
            return 1;
        }
    };
    let conn = get_ref(conn);
    let pos_values = (*in_positional_values).clone();
    match RT.block_on(conn.query(sql, libsql::params::Params::Positional(pos_values))) {
        Ok(rows) => {
            *out_rows = Box::leak(Box::from(rows));
            0
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            2
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_query_named(
    conn: *const libsql::Connection,
    sql: *const std::ffi::c_char,
    in_named_values: *const Vec<(String, libsql::Value)>,
    out_rows: *mut *const libsql::Rows,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!conn.is_null());
    debug_assert!(!sql.is_null());
    debug_assert!(!in_named_values.is_null());

    let sql = unsafe { std::ffi::CStr::from_ptr(sql) };
    let sql = match sql.to_str() {
        Ok(sql) => sql,
        Err(e) => {
            set_err_msg(format!("Wrong SQL: {}", e), out_err_msg);
            return 1;
        }
    };
    let conn = get_ref(conn);
    let pos_values = (*in_named_values).clone();
    match RT.block_on(conn.query(sql, libsql::params::Params::Named(pos_values))) {
        Ok(rows) => {
            *out_rows = Box::leak(Box::from(rows));
            0
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            2
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_execute_none(
    conn: *const libsql::Connection,
    sql: *const std::ffi::c_char,
    out_rows_change: *mut std::ffi::c_ulong,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!conn.is_null());
    debug_assert!(!sql.is_null());

    let sql = unsafe { std::ffi::CStr::from_ptr(sql) };
    let sql = match sql.to_str() {
        Ok(sql) => sql,
        Err(e) => {
            set_err_msg(format!("Wrong SQL: {}", e), out_err_msg);
            return 1;
        }
    };
    let conn = get_ref(conn);
    match RT.block_on(conn.execute(sql, libsql::params::Params::None)) {
        Ok(rows_change) => {
            *out_rows_change = rows_change;
            0
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            2
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_execute_positional(
    conn: *const libsql::Connection,
    sql: *const std::ffi::c_char,
    in_positional_values: *const Vec<libsql::Value>,
    out_rows_change: *mut std::ffi::c_ulong,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!conn.is_null());
    debug_assert!(!sql.is_null());
    debug_assert!(!in_positional_values.is_null());

    let sql = unsafe { std::ffi::CStr::from_ptr(sql) };
    let sql = match sql.to_str() {
        Ok(sql) => sql,
        Err(e) => {
            set_err_msg(format!("Wrong SQL: {}", e), out_err_msg);
            return 1;
        }
    };
    let conn = get_ref(conn);
    let pos_values = (*in_positional_values).clone();
    match RT.block_on(conn.execute(sql, libsql::params::Params::Positional(pos_values))) {
        Ok(rows_change) => {
            *out_rows_change = rows_change;
            0
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            2
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_execute_named(
    conn: *const libsql::Connection,
    sql: *const std::ffi::c_char,
    in_named_values: *const Vec<(String, libsql::Value)>,
    out_rows_change: *mut std::ffi::c_ulong,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!conn.is_null());
    debug_assert!(!sql.is_null());
    debug_assert!(!in_named_values.is_null());

    let sql = unsafe { std::ffi::CStr::from_ptr(sql) };
    let sql = match sql.to_str() {
        Ok(sql) => sql,
        Err(e) => {
            set_err_msg(format!("Wrong SQL: {}", e), out_err_msg);
            return 1;
        }
    };
    let conn = get_ref(conn);
    let pos_values = (*in_named_values).clone();
    match RT.block_on(conn.execute(sql, libsql::params::Params::Named(pos_values))) {
        Ok(rows_change) => {
            *out_rows_change = rows_change;
            0
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            2
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_execute_batch(
    conn: *const libsql::Connection,
    sql: *const std::ffi::c_char,
    out_batch_rows: *mut *const libsql::BatchRows,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    let sql = unsafe { std::ffi::CStr::from_ptr(sql) };
    let sql = match sql.to_str() {
        Ok(sql) => sql,
        Err(e) => {
            set_err_msg(format!("Wrong SQL: {}", e), out_err_msg);
            return 1;
        }
    };
    let conn = get_ref(conn);
    match RT.block_on(conn.execute_batch(sql)) {
        Ok(b_rows) => {
            *out_batch_rows = Box::leak(Box::new(b_rows)) as *const libsql::BatchRows;
            0
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            2
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_changes(conn: *const libsql::Connection) -> u64 {
    debug_assert!(!conn.is_null());

    return get_ref(conn).changes();
}

#[no_mangle]
pub unsafe extern "C" fn libsql_last_insert_rowid(conn: *const libsql::Connection) -> i64 {
    debug_assert!(!conn.is_null());

    return get_ref(conn).last_insert_rowid();
}

#[no_mangle]
pub unsafe extern "C" fn libsql_reset(
    conn: *const libsql::Connection,
    _out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!conn.is_null());

    let conn = get_ref(conn);
    RT.block_on(conn.reset());
    0
}

#[no_mangle]
pub unsafe extern "C" fn libsql_disconnect(conn: *mut libsql::Connection) {
    if conn.is_null() {
        return;
    }
    let conn = Box::from_raw(conn);
    RT.spawn_blocking(|| {
        drop(conn);
    });
}

#[no_mangle]
pub unsafe extern "C" fn libsql_prepare(
    conn: *const libsql::Connection,
    sql: *const std::ffi::c_char,
    out_stmt: *mut *const libsql::Statement,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!conn.is_null());
    debug_assert!(!sql.is_null());

    let sql = unsafe { std::ffi::CStr::from_ptr(sql) };
    let sql = match sql.to_str() {
        Ok(sql) => sql,
        Err(e) => {
            set_err_msg(format!("Wrong SQL: {}", e), out_err_msg);
            return 1;
        }
    };
    let conn = get_ref(conn);
    match RT.block_on(conn.prepare(sql)) {
        Ok(stmt) => {
            *out_stmt = Box::leak(Box::new(stmt)) as *const libsql::Statement;
        }
        Err(e) => {
            set_err_msg(format!("Error preparing statement: {}", e), out_err_msg);
            return 1;
        }
    };
    0
}

//////////////////////////////////////////////
///////////// NAMED VALUES ///////////////////

#[no_mangle]
pub unsafe extern "C" fn libsql_make_namedvalues(
    named_vals: *mut *const Vec<(String, libsql::Value)>,
) {
    *named_vals = Box::leak(Box::new(Vec::new())) as *const Vec<(String, libsql::Value)>;
}

#[no_mangle]
pub unsafe extern "C" fn libsql_free_namedvalues(named_vals: *mut Vec<(String, libsql::Value)>) {
    if named_vals.is_null() {
        return;
    }
    let _ = Box::from_raw(named_vals);
}

unsafe fn named_helper<T>(
    named_vals: *mut Vec<(String, libsql::Value)>,
    name: *const std::ffi::c_char,
    value: T,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int
where
    libsql::Value: From<T>,
{
    debug_assert!(!named_vals.is_null());
    debug_assert!(!name.is_null());

    let name = unsafe { std::ffi::CStr::from_ptr(name) };
    let name = match name.to_str() {
        Ok(v) => v,
        Err(e) => {
            set_err_msg(format!("Wrong named string: {}", e), out_err_msg);
            return 1;
        }
    };
    let named_vals = get_mut_ref(named_vals);

    // TODO: I'm no checking duplicates. Check what would happend with dups.
    named_vals.push((name.to_string(), value.into()));
    0
}

#[no_mangle]
pub unsafe extern "C" fn libsql_named_bind_int(
    named_vals: *mut Vec<(String, libsql::Value)>,
    name: *const std::ffi::c_char,
    value: std::ffi::c_longlong,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    return named_helper(named_vals, name, value, out_err_msg);
}

#[no_mangle]
pub unsafe extern "C" fn libsql_named_bind_float(
    named_vals: *mut Vec<(String, libsql::Value)>,
    name: *const std::ffi::c_char,
    value: std::ffi::c_double,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    return named_helper(named_vals, name, value, out_err_msg);
}

#[no_mangle]
pub unsafe extern "C" fn libsql_named_bind_null(
    named_vals: *mut Vec<(String, libsql::Value)>,
    name: *const std::ffi::c_char,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    return named_helper(named_vals, name, libsql::Value::Null, out_err_msg);
}

#[no_mangle]
pub unsafe extern "C" fn libsql_named_bind_string(
    named_vals: *mut Vec<(String, libsql::Value)>,
    name: *const std::ffi::c_char,
    value: *const std::ffi::c_char,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!value.is_null());

    let value = unsafe { std::ffi::CStr::from_ptr(value) };
    let value = match value.to_str() {
        Ok(v) => v,
        Err(e) => {
            set_err_msg(format!("Wrong value string: {}", e), out_err_msg);
            return 1;
        }
    };
    return named_helper(named_vals, name, value, out_err_msg);
}

#[no_mangle]
pub unsafe extern "C" fn libsql_named_bind_blob(
    named_vals: *mut Vec<(String, libsql::Value)>,
    name: *const std::ffi::c_char,
    value: *const std::ffi::c_uchar,
    value_len: std::ffi::c_int,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!value.is_null());

    let value_len: usize = match value_len.try_into() {
        Ok(v) => v,
        Err(e) => {
            set_err_msg(format!("Wrong param value len: {}", e), out_err_msg);
            return 2;
        }
    };
    let value = unsafe { core::slice::from_raw_parts(value, value_len) };
    let value = Vec::from(value);
    return named_helper(named_vals, name, value, out_err_msg);
}

///////////////////////////////////////////////
//////////// POSITIONAL VALUES ////////////////

#[no_mangle]
pub unsafe extern "C" fn libsql_make_positional_values(pos_values: *mut *const Vec<libsql::Value>) {
    *pos_values = Box::leak(Box::new(Vec::new())) as *const Vec<libsql::Value>;
}

#[no_mangle]
pub unsafe extern "C" fn libsql_free_positional_values(pos_values: *mut Vec<libsql::Value>) {
    if pos_values.is_null() {
        return;
    }
    let _ = Box::from_raw(pos_values);
}

unsafe fn positional_helper<T>(
    pos_values: *mut Vec<libsql::Value>,
    idx: std::ffi::c_uint,
    value: T,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int
where
    libsql::Value: From<T>,
{
    debug_assert!(!pos_values.is_null());

    let idx: usize = match idx.try_into() {
        Ok(x) => x,
        Err(e) => {
            set_err_msg(format!("Wrong param index: {}", e), out_err_msg);
            return 1;
        }
    };
    let pos_values = get_mut_ref(pos_values);
    if pos_values.len() <= idx {
        pos_values.resize(idx + 1, libsql::Value::Null);
    }
    pos_values[idx] = value.into();
    return 0;
}

#[no_mangle]
pub unsafe extern "C" fn libsql_positional_bind_int(
    pos_values: *mut Vec<libsql::Value>,
    idx: std::ffi::c_uint,
    value: std::ffi::c_longlong,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    return positional_helper(pos_values, idx, value, out_err_msg);
}

#[no_mangle]
pub unsafe extern "C" fn libsql_positional_bind_float(
    pos_values: *mut Vec<libsql::Value>,
    idx: std::ffi::c_uint,
    value: std::ffi::c_double,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    return positional_helper(pos_values, idx, value, out_err_msg);
}

#[no_mangle]
pub unsafe extern "C" fn libsql_positional_bind_null(
    pos_values: *mut Vec<libsql::Value>,
    idx: std::ffi::c_uint,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    return positional_helper(pos_values, idx, libsql::Value::Null, out_err_msg);
}

#[no_mangle]
pub unsafe extern "C" fn libsql_positional_bind_string(
    pos_values: *mut Vec<libsql::Value>,
    idx: std::ffi::c_uint,
    value: *const std::ffi::c_char,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!value.is_null());

    let value = unsafe { std::ffi::CStr::from_ptr(value) };
    let value = match value.to_str() {
        Ok(v) => v,
        Err(e) => {
            set_err_msg(format!("Wrong param value: {}", e), out_err_msg);
            return 2;
        }
    };
    return positional_helper(pos_values, idx, value.to_string(), out_err_msg);
}

#[no_mangle]
pub unsafe extern "C" fn libsql_positional_bind_blob(
    pos_values: *mut Vec<libsql::Value>,
    idx: std::ffi::c_uint,
    value: *const std::ffi::c_uchar,
    value_len: std::ffi::c_int,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!value.is_null());

    let value_len: usize = match value_len.try_into() {
        Ok(v) => v,
        Err(e) => {
            set_err_msg(format!("Wrong param value len: {}", e), out_err_msg);
            return 2;
        }
    };
    let value = unsafe { core::slice::from_raw_parts(value, value_len) };
    let value = Vec::from(value);
    return positional_helper(pos_values, idx, value, out_err_msg);
}

////////////////////////////////////////////////
///////////////// STATEMENTS ///////////////////

#[no_mangle]
pub unsafe extern "C" fn libsql_query_stmt(
    stmt: *mut libsql::Statement,
    out_rows: *mut *const libsql::Rows,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!stmt.is_null());

    let stmt = get_mut_ref(stmt);

    match RT.block_on(stmt.query(libsql::params::Params::None)) {
        Ok(rows_) => {
            *out_rows = Box::leak(Box::new(rows_)) as *const libsql::Rows;
            return 0;
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            return 1;
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_query_stmt_positional(
    stmt: *mut libsql::Statement,
    pos_values: *const Vec<libsql::Value>,
    out_rows: *mut *const libsql::Rows,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!stmt.is_null());
    debug_assert!(!pos_values.is_null());

    let stmt = get_mut_ref(stmt);
    let pos_values = get_ref(pos_values);

    match RT.block_on(stmt.query(libsql::params::Params::Positional(pos_values.clone()))) {
        Ok(rows_) => {
            *out_rows = Box::leak(Box::new(rows_)) as *const libsql::Rows;
            return 0;
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            return 1;
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_query_stmt_named(
    stmt: *mut libsql::Statement,
    named_values: *const Vec<(String, libsql::Value)>,
    out_rows: *mut *const libsql::Rows,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!stmt.is_null());
    debug_assert!(!named_values.is_null());

    let stmt = get_mut_ref(stmt);
    let named_values = get_ref(named_values);

    match RT.block_on(stmt.query(libsql::params::Params::Named(named_values.clone()))) {
        Ok(rows_) => {
            *out_rows = Box::leak(Box::new(rows_)) as *const libsql::Rows;
            return 0;
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            return 1;
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_execute_stmt(
    stmt: *mut libsql::Statement,
    affected_rows: *mut std::ffi::c_ulong,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!stmt.is_null());

    let stmt = get_mut_ref(stmt);

    return match RT.block_on(stmt.execute(libsql::params::Params::None)) {
        Ok(rows) => {
            *affected_rows = rows as u64;
            0
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            2
        }
    };
}

#[no_mangle]
pub unsafe extern "C" fn libsql_execute_stmt_positional(
    stmt: *mut libsql::Statement,
    pos_values: *const Vec<libsql::Value>,
    affected_rows: *mut std::ffi::c_ulong,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!stmt.is_null());
    debug_assert!(!pos_values.is_null());

    let stmt = get_mut_ref(stmt);
    let pos_values = get_ref(pos_values);

    return match RT.block_on(stmt.execute(libsql::params::Params::Positional(pos_values.clone()))) {
        Ok(rows) => {
            *affected_rows = rows as u64;
            0
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            2
        }
    };
}

#[no_mangle]
pub unsafe extern "C" fn libsql_execute_stmt_named(
    stmt: *mut libsql::Statement,
    named_values: *const Vec<(String, libsql::Value)>,
    affected_rows: *mut std::ffi::c_ulong,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!stmt.is_null());
    debug_assert!(!named_values.is_null());

    let stmt = get_mut_ref(stmt);
    let named_values = get_ref(named_values);

    return match RT.block_on(stmt.execute(libsql::params::Params::Named(named_values.clone()))) {
        Ok(rows) => {
            *affected_rows = rows as u64;
            0
        }
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            2
        }
    };
}

#[no_mangle]
pub unsafe extern "C" fn libsql_run_stmt(
    stmt: *mut libsql::Statement,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!stmt.is_null());

    let stmt = get_mut_ref(stmt);

    return match RT.block_on(stmt.run(libsql::params::Params::None)) {
        Ok(_) => 0,
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            1
        }
    };
}

#[no_mangle]
pub unsafe extern "C" fn libsql_run_stmt_positional(
    stmt: *mut libsql::Statement,
    pos_values: *const Vec<libsql::Value>,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!stmt.is_null());
    debug_assert!(!pos_values.is_null());

    let stmt = get_mut_ref(stmt);
    let pos_values = get_ref(pos_values);

    return match RT.block_on(stmt.run(libsql::params::Params::Positional(pos_values.clone()))) {
        Ok(_) => 0,
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            1
        }
    };
}

#[no_mangle]
pub unsafe extern "C" fn libsql_run_stmt_named(
    stmt: *mut libsql::Statement,
    named_values: *const Vec<(String, libsql::Value)>,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!stmt.is_null());
    debug_assert!(!named_values.is_null());

    let stmt = get_mut_ref(stmt);
    let named_values = get_ref(named_values);

    return match RT.block_on(stmt.run(libsql::params::Params::Named(named_values.clone()))) {
        Ok(_) => 0,
        Err(e) => {
            set_err_msg(format!("Error executing statement: {}", e), out_err_msg);
            1
        }
    };
}

#[no_mangle]
pub unsafe extern "C" fn libsql_finalize_stmt(
    stmt: *mut libsql::Statement,
    _out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!stmt.is_null());

    let stmt = get_mut_ref(stmt);
    stmt.finalize();
    return 0;
}

#[no_mangle]
pub unsafe extern "C" fn libsql_reset_stmt(
    stmt: *mut libsql::Statement,
    _out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!stmt.is_null());

    let stmt = get_mut_ref(stmt);
    stmt.reset();
    return 0;
}

#[no_mangle]
pub unsafe extern "C" fn libsql_free_stmt(stmt: *mut libsql::Statement) {
    if stmt.is_null() {
        return;
    }
    let _ = Box::from_raw(stmt);
}

///////////////////////////////////////
////////////// ROWS ///////////////////

#[no_mangle]
pub unsafe extern "C" fn libsql_free_rows(res: *mut libsql::Rows) {
    if res.is_null() {
        return;
    }
    let _ = Box::from_raw(res);
}

#[no_mangle]
pub unsafe extern "C" fn libsql_free_rows_future(res: *mut libsql::RowsFuture) {
    if res.is_null() {
        return;
    }
    let mut res = Box::from_raw(res);
    res.wait().unwrap();
}

#[no_mangle]
pub unsafe extern "C" fn libsql_wait_result(res: *mut libsql::RowsFuture) {
    debug_assert!(!res.is_null());

    let mut res = Box::from_raw(res);
    res.wait().unwrap();
}

#[no_mangle]
pub unsafe extern "C" fn libsql_column_count(res: *const libsql::Rows) -> std::ffi::c_int {
    debug_assert!(!res.is_null());

    get_ref(res).column_count()
}

#[no_mangle]
pub unsafe extern "C" fn libsql_column_name(
    res: *const libsql::Rows,
    col: std::ffi::c_int,
    out_name: *mut *const std::ffi::c_char,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!res.is_null());

    let res = get_ref(res);
    if col >= res.column_count() {
        set_err_msg(
            format!(
                "Column index too big - got index {} with {} columns",
                col,
                res.column_count()
            ),
            out_err_msg,
        );
        return 1;
    }
    let name = res.column_name(col);

    if name.is_none() {
        set_err_msg("Column should have valid index".to_string(), out_err_msg);
        return 1;
    }

    match std::ffi::CString::new(name.unwrap()) {
        Ok(name) => {
            *out_name = name.into_raw();
            0
        }
        Err(e) => {
            set_err_msg(format!("Invalid name: {}", e), out_err_msg);
            1
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_column_type(
    res: *const libsql::Rows,
    row_: *const libsql::Row,
    col: std::ffi::c_int,
    out_type: *mut std::ffi::c_int,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!res.is_null());
    debug_assert!(!row_.is_null());

    let res = get_ref(res);
    if col >= res.column_count() {
        set_err_msg(
            format!(
                "Column index too big - got index {} with {} columns",
                col,
                res.column_count()
            ),
            out_err_msg,
        );
        return 1;
    }

    let row_ = get_ref(row_);
    match row_.get_value(col) {
        Ok(libsql::Value::Null) => {
            *out_type = types::LIBSQL_NULL as i32;
        }
        Ok(libsql::Value::Text(_)) => {
            *out_type = types::LIBSQL_TEXT as i32;
        }
        Ok(libsql::Value::Integer(_)) => {
            *out_type = types::LIBSQL_INT as i32;
        }
        Ok(libsql::Value::Real(_)) => {
            *out_type = types::LIBSQL_FLOAT as i32;
        }
        Ok(libsql::Value::Blob(_)) => {
            *out_type = types::LIBSQL_BLOB as i32;
        }
        Err(e) => {
            set_err_msg(format!("Error fetching value: {e}"), out_err_msg);
            return 2;
        }
    };
    0
}

#[no_mangle]
pub unsafe extern "C" fn libsql_next_row(
    res: *mut libsql::Rows,
    out_row: *mut *const libsql::Row,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!res.is_null());

    let res = get_mut_ref(res);
    let res = RT.block_on(res.next());
    match res {
        Ok(Some(row_)) => {
            *out_row = Box::leak(Box::new(row_)) as *const libsql::Row;
            0
        }
        Ok(None) => {
            *out_row = std::ptr::null();
            0
        }
        Err(e) => {
            *out_row = std::ptr::null();
            set_err_msg(format!("Error fetching next row: {}", e), out_err_msg);
            1
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_free_row(res: *mut libsql::Row) {
    if res.is_null() {
        return;
    }
    let _ = Box::from_raw(res);
}

#[no_mangle]
pub unsafe extern "C" fn libsql_get_string(
    res: *const libsql::Row,
    col: std::ffi::c_int,
    out_value: *mut *const std::ffi::c_char,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!res.is_null());

    let res = get_ref(res);
    match res.get_value(col) {
        Ok(libsql::Value::Text(s)) => {
            *out_value = translate_string(s);
            0
        }
        Ok(_) => {
            set_err_msg("Value not a string".into(), out_err_msg);
            1
        }
        Err(e) => {
            set_err_msg(format!("Error fetching value: {e}"), out_err_msg);
            2
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_free_string(ptr: *const std::ffi::c_char) {
    if !ptr.is_null() {
        let _ = unsafe { std::ffi::CString::from_raw(ptr as *mut _) };
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_get_int(
    res: *const libsql::Row,
    col: std::ffi::c_int,
    out_value: *mut std::ffi::c_longlong,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!res.is_null());

    let res = get_ref(res);
    match res.get_value(col) {
        Ok(libsql::Value::Integer(i)) => {
            *out_value = i;
            0
        }
        Ok(_) => {
            set_err_msg("Value not an integer".into(), out_err_msg);
            1
        }
        Err(e) => {
            set_err_msg(format!("Error fetching value: {e}"), out_err_msg);
            2
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_get_float(
    res: *const libsql::Row,
    col: std::ffi::c_int,
    out_value: *mut std::ffi::c_double,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!res.is_null());

    let res = get_ref(res);
    match res.get_value(col) {
        Ok(libsql::Value::Real(f)) => {
            *out_value = f;
            0
        }
        Ok(_) => {
            set_err_msg("Value not a float".into(), out_err_msg);
            1
        }
        Err(e) => {
            set_err_msg(format!("Error fetching value: {e}"), out_err_msg);
            2
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_get_blob(
    res: *const libsql::Row,
    col: std::ffi::c_int,
    out_blob: *mut blob,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!res.is_null());

    let res = get_ref(res);
    match res.get_value(col) {
        Ok(libsql::Value::Blob(v)) => {
            let len: i32 = v.len().try_into().unwrap();
            let buf = v.into_boxed_slice();
            let data = buf.as_ptr();
            std::mem::forget(buf);
            *out_blob = blob {
                ptr: data as *const std::ffi::c_char,
                len,
            };
            0
        }
        Ok(_) => {
            set_err_msg("Value not a float".into(), out_err_msg);
            1
        }
        Err(e) => {
            set_err_msg(format!("Error fetching value: {}", e), out_err_msg);
            2
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_free_blob(b: blob) {
    if !b.ptr.is_null() {
        let ptr =
            unsafe { std::slice::from_raw_parts_mut(b.ptr as *mut i8, b.len.try_into().unwrap()) };
        let _ = unsafe { Box::from_raw(ptr) };
    }
}

///////////////////////////////////////////////////////
//////////////// TRANSACTION //////////////////////////

#[no_mangle]
pub unsafe extern "C" fn libsql_commit_transaction(
    transaction: *mut libsql::Transaction,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!transaction.is_null());

    let transaction = Box::from_raw(transaction);

    match RT.block_on((*transaction).commit()) {
        Ok(()) => return 0,
        Err(e) => {
            set_err_msg(format!("Transaction Commmit: {e}"), out_err_msg);
            return 1;
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_rollback_transaction(
    transaction: *mut libsql::Transaction,
    out_err_msg: *mut *const std::ffi::c_char,
) -> std::ffi::c_int {
    debug_assert!(!transaction.is_null());

    let transaction = Box::from_raw(transaction);

    match RT.block_on((*transaction).rollback()) {
        Ok(()) => return 0,
        Err(e) => {
            set_err_msg(format!("Transaction Rollback: {e}"), out_err_msg);
            return 1;
        }
    }
}

// This is quite weird a transaction being able to create new transactions and act as a connection,
// Until futher research this doesnt look as a good design.
#[no_mangle]
pub unsafe extern "C" fn libsql_connection_transaction(
    transaction: *const libsql::Transaction,
    connection: *mut *const libsql::Connection,
) -> std::ffi::c_int {
    debug_assert!(!transaction.is_null());

    *connection = get_ref(transaction).deref();
    return 0;
}

//////////////////////////////////////////////////////
//////////////// BATCH_ROWS //////////////////////////

#[no_mangle]
pub unsafe extern "C" fn libsql_next_stmt_row_batchrows(
    batchrows: *mut libsql::BatchRows,
    out_rows: *mut *const libsql::Rows,
) -> std::ffi::c_int {
    debug_assert!(!batchrows.is_null());

    match get_mut_ref(batchrows).next_stmt_row() {
        Some(Some(rows)) => {
            *out_rows = Box::leak(Box::from(rows));
            return 0;
        }
        Some(None) => {
            *out_rows = null();
            return 0
        }
        None => {
            *out_rows = null();
            return 1;
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn libsql_free_batchrows(
    batchrows: *mut libsql::BatchRows) {
    if batchrows.is_null() {
        return
    }
    let _ = Box::from_raw(batchrows);
}
