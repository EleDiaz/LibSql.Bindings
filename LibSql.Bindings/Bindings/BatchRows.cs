
namespace LibSql.Bindings;

public partial class BatchRows
{
    IntPtr _batchRows;
    
    // TODO:
    public async Task<Rows?> NextStmtRow() {
        throw new NotImplementedException();
    }


}
