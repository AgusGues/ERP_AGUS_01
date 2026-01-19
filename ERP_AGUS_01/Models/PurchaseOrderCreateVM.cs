namespace ERP_AGUS_01.Models
{
    public class PurchaseOrderCreateVM
    {
        public int SupplierId { get; set; }
        public List<PurchaseOrderItemVM> Items { get; set; } = new List<PurchaseOrderItemVM>();
    }
}
