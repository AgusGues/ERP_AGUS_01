namespace ERP_AGUS_01.Models
{
    public class PurchaseOrderTermVM
    {
        public int TermNo { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Percentage { get; set; }
        public decimal Amount { get; set; }
    }
}
