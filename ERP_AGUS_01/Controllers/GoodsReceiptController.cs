using ERP_AGUS_01.Data;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;
using ERP_AGUS_01.Models;

namespace ERP_AGUS_01.Controllers
{
    public class GoodsReceiptController : Controller
    {
        private readonly DbHelper _db;

        public GoodsReceiptController(DbHelper db)
        {
            _db = db;
        }

        // INDEX
        public IActionResult Index()
        {
            var dt = _db.ExecuteQuery(@"
            SELECT 
            p.PONumber,
            pod.PODetailId,
            pod.POId,
            i.ItemName,
            pod.Qty AS POQty,
            ISNULL(SUM(grd.Qty),0) AS ReceivedQty,
            (pod.Qty - ISNULL(SUM(grd.Qty),0)) AS OutstandingQty
        FROM PurchaseOrderDetails pod
        JOIN Items i ON pod.ItemId = i.ItemId
        Join PurchaseOrders p on pod.POId=p.POId
        LEFT JOIN GoodsReceiptDetails grd 
            ON pod.PODetailId = grd.PODetailId
        GROUP BY pod.PODetailId, pod.POId, i.ItemName, pod.Qty,p.PONumber
        HAVING (pod.Qty - ISNULL(SUM(grd.Qty),0)) > 0
        ");

            ViewBag.Warehouses = _db.ExecuteQuery("SELECT * FROM Warehouses");
            ViewBag.Locations = _db.ExecuteQuery("SELECT * FROM WarehouseLocations");

            return View(dt);
        }

        // SAVE GR (DARI MODAL)
        [HttpPost]
        public IActionResult Save(int PODetailId, decimal Qty, int WarehouseId, int LocationId)
        {
            int grId = Convert.ToInt32(
                _db.ExecuteScalar("INSERT INTO GoodsReceipts(ReceiptDate) VALUES (GETDATE()); SELECT SCOPE_IDENTITY();")
            );

            _db.ExecuteNonQuery(@"
        INSERT INTO GoodsReceiptDetails
        (GRId, PODetailId, Qty, WarehouseId, LocationId)
        VALUES
        (@GRId, @PODetailId, @Qty, @WarehouseId, @LocationId)
    ",
            new[]
            {
        new SqlParameter("@GRId", grId),
        new SqlParameter("@PODetailId", PODetailId),
        new SqlParameter("@Qty", Qty),
        new SqlParameter("@WarehouseId", WarehouseId),
        new SqlParameter("@LocationId", LocationId)
            });

            return RedirectToAction("Index");
        }

    }


}


