namespace SharedServices.Enum
{
    /// <summary>Approval request types, equal to the non-direct permission except product.transfer, gated by route.create.</summary>
    public static class RequestType
    {
        public const string CreateProduct = "product.create";
        public const string UpdateProduct = "product.update";
        public const string DeleteProduct = "product.delete";
        public const string TransferProduct = "product.transfer";
        public const string UpdateRoute = "route.update";
        public const string DeleteRoute = "route.delete";
    }
}