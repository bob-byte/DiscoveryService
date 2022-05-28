namespace LUC.Interfaces.OutputContracts
{
    public class DeleteResponse : BaseResponse
    {
        public DeleteResponse() : base()
        {
        }

        public DeleteResponse( System.Boolean isSuccess, System.Boolean isForbidden, System.String message ) : base( isSuccess, isForbidden, message )
        {
        }
    }
}
