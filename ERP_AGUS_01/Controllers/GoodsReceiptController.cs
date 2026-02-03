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
            using var tran = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);

            try
            {
                // ===============================
                // 1️⃣ VALIDASI INPUT
                // ===============================
                if (Qty <= 0)
                    throw new Exception("Qty receipt harus lebih dari 0");

                if (WarehouseId <= 0 || LocationId <= 0)
                    throw new Exception("Warehouse dan Location wajib diisi");

                // ===============================
                // 2️⃣ VALIDASI PO DETAIL + LOCK
                // FIX: pastikan PODetail benar milik PO
                // ===============================
                DataTable poDetail = _db.ExecuteQuery(@"
            SELECT ItemId, Qty
            FROM PurchaseOrderDetails WITH (UPDLOCK, HOLDLOCK)
            WHERE PODetailId=@id AND POId=@po",
                    new[]
                    {
                new SqlParameter("@id", PODetailId),
                new SqlParameter("@po", POId)
                    },
                    conn, tran);

                if (poDetail.Rows.Count == 0)
                    throw new Exception("PO Detail tidak valid");

                int itemId = Convert.ToInt32(poDetail.Rows[0]["ItemId"]);
                decimal poQty = Convert.ToDecimal(poDetail.Rows[0]["Qty"]);

                // ===============================
                // 3️⃣ HITUNG OUTSTANDING PER ITEM
                // ===============================
                decimal receivedQty = Convert.ToDecimal(
                    _db.ExecuteScalar(@"
                SELECT ISNULL(SUM(Qty),0)
                FROM GoodsReceiptDetails WITH (UPDLOCK)
                WHERE PODetailId=@id",
                        new[] { new SqlParameter("@id", PODetailId) },
                        conn, tran)
                );

                decimal outstanding = poQty - receivedQty;

                if (Qty > outstanding)
                    throw new Exception("Qty receipt melebihi sisa PO");

                // ===============================
                // 4️⃣ GR HEADER (1 PER TRANSAKSI)
                // FIX: buat sekali saja
                // ===============================
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

                // ===============================
                // 5️⃣ GR DETAIL
                // ===============================
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
                    conn, tran);

                // ===============================
                // 6️⃣ STOCK CARD
                // ===============================
                _db.ExecuteNonQuery(@"
            INSERT INTO StockCards
            (ItemId, WarehouseId, LocationId, TransDate, TransType, QtyIn, QtyOut, ReferenceNo)
            VALUES
            (@ItemId, @WarehouseId, @LocationId, GETDATE(), 'GR', @Qty, 0,
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

                // ===============================
                // 7️⃣ STOCK UPSERT (AMAN)
                // ===============================
                int updated = _db.ExecuteNonQuery(@"
            UPDATE Stocks WITH (UPDLOCK, HOLDLOCK)
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

                if (updated == 0)
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

                // ===============================
                // 8️⃣ CLOSE PO (PER ITEM AMAN)
                // FIX: hitung outstanding per item
                // ===============================
                decimal totalOutstanding = Convert.ToDecimal(
                    _db.ExecuteScalar(@"
                SELECT SUM(x.Outstanding)
                FROM (
                    SELECT d.Qty - ISNULL(SUM(gr.Qty),0) AS Outstanding
                    FROM PurchaseOrderDetails d
                    LEFT JOIN GoodsReceiptDetails gr
                        ON d.PODetailId = gr.PODetailId
                    WHERE d.POId=@POId
                    GROUP BY d.PODetailId, d.Qty
                ) x",
                        new[] { new SqlParameter("@POId", POId) },
                        conn, tran)
                );

                if (totalOutstanding <= 0)
                {
                    _db.ExecuteNonQuery(@"
                UPDATE PurchaseOrders
                SET Status='CLOSED'
                WHERE POId=@POId",
                        new[] { new SqlParameter("@POId", POId) },
                        conn, tran);
                }

                tran.Commit();
                TempData["Success"] = "Goods Receipt berhasil disimpan";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

    }
}
