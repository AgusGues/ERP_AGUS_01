using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;
using ERP_AGUS_01.Data;

namespace ERP_AGUS_01.Controllers
{
    public class StockController : Controller
    {
        private readonly DbHelper _db;

        public StockController(DbHelper db)
        {
            _db = db;
        }
        public IActionResult Index()
        {
            DataTable dt = _db.ExecuteQuery(@"
            SELECT 
            s.StockId,
            s.ItemId,
            s.WarehouseId,
            s.LocationId,
            i.ItemName,
            w.WarehouseName,
            wl.LocationCode,
            s.Qty
        FROM Stocks s
        INNER JOIN Items i ON s.ItemId = i.ItemId
        INNER JOIN Warehouses w ON s.WarehouseId = w.WarehouseId
        INNER JOIN WarehouseLocations wl ON s.WarehouseId = wl.WarehouseId
        ORDER BY i.ItemName;
        ");

            return View(dt);
        }
    }
}
