using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;
using ERP_AGUS_01.Data;
using ERP_AGUS_01.Models;

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
                i.ItemName,
                wFrom.WarehouseName AS FromWarehouse,
                wTo.WarehouseName   AS ToWarehouse,
                lFrom.LocationCode AS FromLocation,
                lTo.LocationCode   AS ToLocation,
                sd.Qty,
                t.Status
            FROM StockTransfers t
            JOIN Warehouses wFrom 
                ON t.FromWarehouseId = wFrom.WarehouseId
            JOIN Warehouses wTo
                ON t.ToWarehouseId = wTo.WarehouseId
            JOIN WarehouseLocations lFrom
                ON t.FromLocationId = lFrom.LocationId
            JOIN WarehouseLocations lTo
                ON t.ToLocationId = lTo.LocationId
            JOIN StockTransferDetails sd on t.TransferId = sd.TransferId
            JOIN Items i on sd.ItemId = i.ItemId
            ORDER BY t.TransferDate DESC;
            ");

            ViewBag.Warehouses = _db.ExecuteQuery("SELECT * FROM Warehouses");
            ViewBag.Locations = _db.ExecuteQuery("SELECT * FROM WarehouseLocations");
            

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

        [HttpPost]
        public IActionResult SaveDraft(StockTransferVM model)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();

            try
            {

                string transferNo = "TRF-" + DateTime.Now.ToString("yyyyMMddHHmmss");

                //1. StockTransfers HEADER
                int transferId = Convert.ToInt32(_db.ExecuteScalar(@"
                        insert into StockTransfers(
                        TransferNumber,
                        TransferDate,
                        FromWarehouseId,
                        ToWarehouseId,
                        FromLocationId,
                        ToLocationId,
                        Status
                        )
                        VALUES
                        (
                        @TransferNumber,
                        GETDATE(),
                        @FromWarehouseId,
                        @ToWarehouseId,
                        @FromLocationId,
                        @ToLocationId,
                        'DRAFT');
                    SELECT SCOPE_IDENTITY();",
                    new[]
                    {
                        new SqlParameter("@TransferNumber",transferNo),
                        new SqlParameter("@FromWarehouseId",model.FromWarehouseId),
                        new SqlParameter("@ToWarehouseId",model.ToWarehouseId),
                        new SqlParameter("@FromLocationId",model.FromLocationId),
                        new SqlParameter("@ToLocationId",model.ToLocationId)
                    },
                    conn, tran)
                    );

                //2. Insert StockTransferDetails
                foreach (var item in model.Items)
                {
                    _db.ExecuteNonQuery(@"
                        INSERT INTO StockTransferDetails
                        (
                            TransferId,
                            ItemId,
                            Qty
                        )
                        VALUES
                        (
                            @TransferId,
                            @ItemId,
                            @Qty
                        )",
                new[]
                {
                    new SqlParameter("@TransferId", transferId),
                    new SqlParameter("@ItemId", item.ItemId),
                    new SqlParameter("@Qty", item.Qty)
                },
                conn, tran);
                }

                tran.Commit();
                TempData["Success"] = "Draft Transfer Stock Berhasil di Simpan";
            }
            catch 
            {
                tran.Rollback();
                throw;
            }

            return RedirectToAction("Index");
        }
    }
}
