namespace ERP_AGUS_01.Models
{
    public class PurchaseOrderDetail
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public decimal Qty { get; set; }
        public decimal Price { get; set; }
    }
}
