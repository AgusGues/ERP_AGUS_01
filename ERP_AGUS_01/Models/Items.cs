namespace ERP_AGUS_01.Models
{
    public class Items
    {
        public int ItemId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string Unit { get; set; }
        public int isActive { get; set; }
        public DateTime createdDate { get; set; }
        public string createdBy { get; set; }
    }
}
