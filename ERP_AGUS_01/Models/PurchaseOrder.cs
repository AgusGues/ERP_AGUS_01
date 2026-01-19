namespace ERP_AGUS_01.Models
{
    public class PurchaseOrder
    {
        public int POId { get; set; }
        public string PONumber { get; set; }
        public DateTime PODate { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string Status { get; set; }
    }
}
