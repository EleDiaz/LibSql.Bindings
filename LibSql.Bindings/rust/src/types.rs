pub const LIBSQL_INT: i8 = 1;
pub const LIBSQL_FLOAT: i8 = 2;
pub const LIBSQL_TEXT: i8 = 3;
pub const LIBSQL_BLOB: i8 = 4;
pub const LIBSQL_NULL: i8 = 5;

pub const LIBSQL_TRANSACTION_DEFERRED: i8 = 1;
pub const LIBSQL_TRANSACTION_IMMEDIATE: i8 = 2;
pub const LIBSQL_TRANSACTION_EXCLUSIVE: i8 = 3;
pub const LIBSQL_TRANSACTION_READONLY: i8 = 4;

#[derive(Clone, Debug)]
#[repr(C)]
pub struct LibSqlConfig {
    pub db_path: *const std::ffi::c_char,
    pub primary_url: *const std::ffi::c_char,
    pub auth_token: *const std::ffi::c_char,
    pub read_your_writes: std::ffi::c_char,
    pub encryption_key: *const std::ffi::c_char,
    pub sync_interval: std::ffi::c_int,
    pub with_webpki: std::ffi::c_char,
}

#[derive(Clone, Debug)]
#[repr(C)]
pub struct blob {
    pub ptr: *const std::ffi::c_char,
    pub len: std::ffi::c_int,
}

#[repr(C)]
pub struct replicated {
    pub frame_no: std::ffi::c_int,
    pub frames_synced: std::ffi::c_int,
}

