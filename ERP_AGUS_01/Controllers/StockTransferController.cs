using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;
using ERP_AGUS_01.Data;

namespace ERP_AGUS_01.Controllers
{
    public class StockTransferController : Controller
    {
        private readonly DbHelper _db;
        public StockTransferController(DbHelper db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            var dt = _db.ExecuteQuery(
                @"
                SELECT 
                t.TransferId,	
                t.TransferNumber,
                t.TransferDate,
                t.FromWarehouseId,
                w1.WarehouseName AS FromWarehouse,
                t.ToWarehouseId,
                w1.WarehouseName AS ToWarehouse,
                t.FromLocationId AS FromLocation,
                t.ToLocationId AS ToLocation,t.Status
            FROM StockTransfers t
            JOIN Warehouses w1 ON t.FromWarehouseId = w1.WarehouseId ORDER BY t.TransferDate DESC;
            ");

            ViewBag.Warehouses = _db.ExecuteQuery("SELECT * FROM Warehouses");
            ViewBag.Locations = _db.ExecuteQuery("SELECT * FROM WarehouseLocations");
            //ViewBag.Items = _db.ExecuteQuery("SELECT * FROM Items");

            return View(dt);
        }

        public IActionResult SearchItem(string term)
        {
            DataTable dt = _db.ExecuteQuery(@"
                                            SELECT TOP 10 ItemId, ItemName
                                            FROM Items
                                            WHERE ItemName LIKE '%' + @term + '%'
                                            ORDER BY ItemName",
                new[] {
                        new SqlParameter("@term", term)
                });

            var data = dt.AsEnumerable().Select(r => new
            {
                label = r["ItemName"].ToString(), // yang tampil
                value = r["ItemId"].ToString()    // yang disimpan
            });

            return Json(data);
        }

        
    }
}
