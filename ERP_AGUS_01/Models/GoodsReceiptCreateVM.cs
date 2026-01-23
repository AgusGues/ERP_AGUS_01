namespace ERP_AGUS_01.Models
{
    public class GoodsReceiptCreateVM
    {
        public int POId { get; set; }
        public int WarehouseId { get; set; }
        public DateTime ReceiptDate { get; set; }

        public List<GoodsReceiptItemVM> Items { get; set; } = new();
    }
}
