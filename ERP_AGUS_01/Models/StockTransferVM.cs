namespace ERP_AGUS_01.Models
{
    public class StockTransferVM
    {
        
        public int FromWarehouseId { get; set; }
        public int FromLocationId { get; set; }
        public int ToWarehouseId { get; set; }
        public int ToLocationId { get; set; }
        public string Status { get; set; }

        public List<StockTransferItemVM> Items { get; set; }
    }
}
