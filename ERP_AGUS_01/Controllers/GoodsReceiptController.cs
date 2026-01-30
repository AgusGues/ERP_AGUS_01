using ERP_AGUS_01.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ERP_AGUS_01.Controllers
{
    public class GoodsReceiptController : Controller
    {
        private readonly DbHelper _db;

        public GoodsReceiptController(DbHelper db)
        {
            _db = db;
        }

        // =========================
        // INDEX (NO TRANSACTION)
        // =========================
        public IActionResult Index()
        {
            var dt = _db.ExecuteQuery(@"
                SELECT
                    p.POId,
                    p.PONumber,
                    d.PODetailId,
                    d.ItemId,
                    i.ItemName,
                    d.Qty - ISNULL(SUM(grd.Qty),0) AS OutstandingQty
                FROM PurchaseOrders p
                JOIN PurchaseOrderDetails d ON p.POId = d.POId
                JOIN Items i ON d.ItemId = i.ItemId
                LEFT JOIN GoodsReceiptDetails grd
                    ON d.PODetailId = grd.PODetailId
                GROUP BY
                    p.POId, p.PONumber,
                    d.PODetailId, d.ItemId,
                    i.ItemName, d.Qty
                HAVING d.Qty - ISNULL(SUM(grd.Qty),0) > 0
                ORDER BY p.PONumber");

            ViewBag.Warehouses = _db.ExecuteQuery("SELECT * FROM Warehouses");
            ViewBag.Locations = _db.ExecuteQuery("SELECT * FROM WarehouseLocations");

            return View(dt);
        }

        [HttpGet]
        public IActionResult GetByPO(int poId)
        {
            DataTable dt = _db.ExecuteQuery(@"
        SELECT
            gr.ReceiptId,
            gr.ReceiptNumber,
            gr.ReceiptDate,
            s.SupplierName,
            i.ItemName,
            grd.Qty
        FROM GoodsReceipts gr
        JOIN PurchaseOrders po ON gr.POId = po.POId
        JOIN Suppliers s ON po.SupplierId = s.SupplierId
        JOIN GoodsReceiptDetails grd on grd.ReceiptId=gr.ReceiptId
        JOIN Items i on grd.ItemId = i.ItemId
        WHERE gr.POId = @POId",
                new[] { new SqlParameter("@POId", poId) }
            );

            var data = dt.AsEnumerable().Select(r => new
            {
                ReceiptId = r["ReceiptId"],
                ReceiptNumber = r["ReceiptNumber"].ToString(),
                ReceiptDate = Convert.ToDateTime(r["ReceiptDate"]).ToString("dd-MM-yyyy"),
                SupplierName = r["SupplierName"].ToString(),
                ItemName = r["ItemName"].ToString(),
                Qty = r.Field<decimal>("Qty")
            });


            return Json(data);
        }

        public IActionResult ModalDetail(int id)
        {
            var dt = _db.ExecuteQuery(@"
        SELECT
            gr.ReceiptNumber,
            gr.ReceiptDate,
            i.ItemName,
            d.Qty,
            l.LocationCode
        FROM GoodsReceiptDetails d
        JOIN GoodsReceipts gr ON d.ReceiptId = gr.ReceiptId
        JOIN PurchaseOrderDetails pod ON d.PODetailId = pod.PODetailId
        JOIN Items i ON pod.ItemId = i.ItemId
        JOIN WarehouseLocations l ON d.LocationId = l.LocationId
        WHERE d.ReceiptId = @id",
                new[] { new SqlParameter("@id", id) }
            );

            return PartialView("_ModalGRDetail", dt);
        }


        // =========================
        // SAVE (TRANSACTION)
        // =========================
        [HttpPost]
        public IActionResult Save(
            int POId,
            int PODetailId,
            decimal Qty,
            int WarehouseId,
            int LocationId)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();

            try
            {
                // 1️⃣ ITEM ID
                int itemId = Convert.ToInt32(
                    _db.ExecuteScalar(@"
                        SELECT ItemId
                        FROM PurchaseOrderDetails
                        WHERE PODetailId=@id",
                        new[] { new SqlParameter("@id", PODetailId) },
                        conn, tran)
                );

                // 2️⃣ GR HEADER
                int receiptId = Convert.ToInt32(
                    _db.ExecuteScalar(@"
                        INSERT INTO GoodsReceipts
                        (ReceiptNumber, ReceiptDate, POId, WarehouseId)
                        VALUES
                        ('GR-' + FORMAT(GETDATE(),'yyyyMMddHHmmss'),
                         GETDATE(), @POId, @WarehouseId);
                        SELECT SCOPE_IDENTITY();",
                        new[]
                        {
                            new SqlParameter("@POId", POId),
                            new SqlParameter("@WarehouseId", WarehouseId)
                        },
                        conn, tran)
                );

                
                // 3️⃣ INSERT GR DETAIL
                _db.ExecuteNonQuery(@"
                INSERT INTO GoodsReceiptDetails
                (ReceiptId, ItemId, PODetailId, Qty, LocationId)
                VALUES
                (@ReceiptId, @ItemId, @PODetailId, @Qty, @LocationId)",
                 new[]
                    {
                    new SqlParameter("@ReceiptId", receiptId),
                    new SqlParameter("@ItemId", itemId),
                    new SqlParameter("@PODetailId", PODetailId),
                    new SqlParameter("@Qty", Qty),
                    new SqlParameter("@LocationId", LocationId)
                    },
                    
                    conn, tran
                );


                // 4️⃣ STOCK CARD
                _db.ExecuteNonQuery(@"
                    INSERT INTO StockCards
                    (
                        ItemId, 
                        WarehouseId, 
                        LocationId, 
                        TransDate, 
                        TransType, 
                        QtyIn, 
                        QtyOut, 
                        ReferenceNo
                    )
                    VALUES
                    (   
                        @ItemId, 
                        @WarehouseId, 
                        @LocationId,
                        GETDATE(), 
                        'GR', 
                        @Qty, 
                        0,
                     (SELECT ReceiptNumber FROM GoodsReceipts WHERE ReceiptId=@ReceiptId))",
                    new[]
                    {
                        new SqlParameter("@ItemId", itemId),
                        new SqlParameter("@WarehouseId", WarehouseId),
                        new SqlParameter("@LocationId", LocationId),
                        new SqlParameter("@Qty", Qty),
                        new SqlParameter("@ReceiptId", receiptId)
                    },
                    conn, tran);

                // 5️⃣ STOCK (UPSERT)
                int exists = Convert.ToInt32(
                    _db.ExecuteScalar(@"
                        SELECT COUNT(*)
                        FROM Stocks
                        WHERE ItemId=@ItemId
                          AND WarehouseId=@WarehouseId
                          AND LocationId=@LocationId",
                        new[]
                        {
                            new SqlParameter("@ItemId", itemId),
                            new SqlParameter("@WarehouseId", WarehouseId),
                            new SqlParameter("@LocationId", LocationId)
                        },
                        conn, tran)
                );

                if (exists > 0)
                {
                    _db.ExecuteNonQuery(@"
                        UPDATE Stocks
                        SET Qty = Qty + @Qty
                        WHERE ItemId=@ItemId
                          AND WarehouseId=@WarehouseId
                          AND LocationId=@LocationId",
                        new[]
                        {
                            new SqlParameter("@Qty", Qty),
                            new SqlParameter("@ItemId", itemId),
                            new SqlParameter("@WarehouseId", WarehouseId),
                            new SqlParameter("@LocationId", LocationId)
                        },
                        conn, tran);
                }
                else
                {
                    _db.ExecuteNonQuery(@"
                        INSERT INTO Stocks
                        (ItemId, WarehouseId, LocationId, Qty)
                        VALUES
                        (@ItemId, @WarehouseId, @LocationId, @Qty)",
                        new[]
                        {
                            new SqlParameter("@ItemId", itemId),
                            new SqlParameter("@WarehouseId", WarehouseId),
                            new SqlParameter("@LocationId", LocationId),
                            new SqlParameter("@Qty", Qty)
                        },
                        conn, tran);
                }

                // 6️⃣ CEK TOTAL OUTSTANDING SELURUH ITEM DALAM PO
                decimal totalOutstanding = Convert.ToDecimal(
                    _db.ExecuteScalar(@"
                                        SELECT SUM(
                                            CASE 
                                                WHEN d.Qty - ISNULL(gr.ReceivedQty, 0) < 0 THEN 0
                                                ELSE d.Qty - ISNULL(gr.ReceivedQty, 0)
                                            END
                                        )
                                        FROM PurchaseOrderDetails d
                                        LEFT JOIN (
                                            SELECT PODetailId, SUM(Qty) AS ReceivedQty
                                            FROM GoodsReceiptDetails
                                            GROUP BY PODetailId
                                        ) gr ON d.PODetailId = gr.PODetailId
                                        WHERE d.POId = @POId",
                        new[] { new SqlParameter("@POId", POId) },
                        conn, tran)
                );

                // 7️⃣ JIKA SEMUA ITEM HABIS → CLOSE PO
                if (totalOutstanding <= 0)
                {
                    _db.ExecuteNonQuery(@"
                                            UPDATE PurchaseOrders
                                            SET Status = 'CLOSED'
                                            WHERE POId = @POId",
                        new[] { new SqlParameter("@POId", POId) },
                        conn, tran
                    );
                }


                tran.Commit();
                TempData["Success"] = "Goods Receipt berhasil disimpan";
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
