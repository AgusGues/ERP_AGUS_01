using ERP_AGUS_01.Data;
using ERP_AGUS_01.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ERP_AGUS_01.Controllers
{
    public class PurchaseOrderController : Controller
    {
        private readonly DbHelper _db;
        public PurchaseOrderController(DbHelper db) => _db = db;

        // ================= INDEX =================
        public IActionResult Index()
        {
            DataTable dt = _db.ExecuteQuery(@"
            SELECT
            p.POId,
            p.PONumber,
            p.PODate,
            p.Status,
            s.SupplierName
            FROM PurchaseOrders p
            JOIN Suppliers s ON p.SupplierId = s.SupplierId
            ORDER BY p.PODate DESC");

            return View(dt);

            
        }

        // ================= CREATE =================
        public IActionResult Create()
        {
            ViewBag.Suppliers = _db.ExecuteQuery(
                "SELECT SupplierId, SupplierName FROM Suppliers");

            ViewBag.Items = _db.ExecuteQuery(
                "SELECT ItemId, ItemName FROM Items");

            return View();
        }


        [HttpPost]
        public IActionResult Create(PurchaseOrderCreateVM model)
        {
            if (!ModelState.IsValid || model.Items.Count == 0)
            {
                TempData["Error"] = "Isi supplier dan minimal satu item.";
                return View(model);
            }

            // Insert PO header
            int poId = Convert.ToInt32(
                _db.ExecuteScalar(
                    "EXEC sp_InsertPO @SupplierId",
                    new[] { new SqlParameter("@SupplierId", model.SupplierId) }
                )
            );

            if (poId <= 0)
                throw new Exception("Gagal membuat PO Header");

            // Insert PO details
            foreach (var item in model.Items)
            {
                if (item.ItemId <= 0) continue;

                _db.ExecuteNonQuery(@"
                                    INSERT INTO PurchaseOrderDetails
                                    (POId, ItemId, Qty, Price)
                                    VALUES (@POId, @ItemId, @Qty, @Price)",
                    new[]
                    {
                    new SqlParameter("@POId", poId),
                    new SqlParameter("@ItemId", item.ItemId),
                    new SqlParameter("@Qty", item.Qty),
                    new SqlParameter("@Price", item.Price)
                    });
            }

            //insert termin pembayaran jika pembayaran menggunakan termin
            if (model.Terms != null && model.Terms.Count > 0)
            {
                // 1️⃣ VALIDASI TOTAL PERSENTASE
                decimal totalPercent = model.Terms
                    .Where(t => t.Percentage > 0)
                    .Sum(t => t.Percentage);

                if (totalPercent != 100)
                {
                    ModelState.AddModelError("", "Total persentase termin harus 100%");
                    return View(model);
                }

                // 2️⃣ INSERT SETELAH VALID
                foreach (var term in model.Terms)
                {
                    if (term.Percentage <= 0)
                        continue;

                    if (term.DueDate == DateTime.MinValue)
                        throw new Exception("Tanggal jatuh tempo termin wajib diisi");

                    _db.ExecuteNonQuery(@"
                                        INSERT INTO PurchaseOrderPaymentTerms
                                        (POId, TermNo, DueDate, Percentage,Amount, Status)
                                        VALUES
                                        (@POId, @TermNo, @DueDate, @Percentage,@Amount, 'OPEN')",
                                        new[]
                                        {
                                            new SqlParameter("@POId", poId),
                                            new SqlParameter("@TermNo", term.TermNo),
                                            new SqlParameter("@DueDate", term.DueDate),
                                            new SqlParameter("@Percentage", term.Percentage),
                                            new SqlParameter("@Amount",term.Amount)
                                        });
                }
            }

            TempData["Success"] = $"PO {poId} berhasil dibuat.";
            return RedirectToAction(nameof(Index));
        }
        
        public JsonResult SearchSupplier(string term)
        {
            DataTable dt = _db.ExecuteQuery(
                @"SELECT TOP 10 SupplierId, SupplierName
                  FROM Suppliers
                  WHERE SupplierName LIKE @q",
                new[] { new SqlParameter("@q", "%" + term + "%") });

            var result = new List<object>();

            foreach (DataRow r in dt.Rows)
            {
                result.Add(new
                {
                    id = Convert.ToInt32(r["SupplierId"]),
                    label = r["SupplierName"].ToString(), // WAJIB string
                    value = r["SupplierName"].ToString()  // WAJIB string
                });
            }

            return Json(result);
        }


        public JsonResult SearchItem(string term)
        {
            DataTable dt = _db.ExecuteQuery(
                @"SELECT TOP 10 ItemId, ItemName
                  FROM Items
                  WHERE IsActive=1 AND ItemName LIKE @q",
                new[] { new SqlParameter("@q", "%" + term + "%") });

            var result = new List<object>();
            foreach (DataRow r in dt.Rows)
            {
                result.Add(new
                {
                    id = r["ItemId"],
                    label = r["ItemName"]
                });
            }
            return Json(result);
        }

        public JsonResult GetLastPrice(int supplierId, int itemId)
        {
            DataTable dt = _db.ExecuteQuery(@"
                SELECT TOP 1 Price
                FROM PurchaseOrderDetails d
                JOIN PurchaseOrders h ON h.POId = d.POId
                WHERE h.SupplierId = @supplier
                  AND d.ItemId = @item
                ORDER BY h.PODate DESC",
                new[]
                {
            new SqlParameter("@supplier", supplierId),
            new SqlParameter("@item", itemId)
                });

            decimal price = 0;
            if (dt.Rows.Count > 0)
                price = Convert.ToDecimal(dt.Rows[0]["Price"]);

            return Json(price);
        }

        public IActionResult Outstanding(int id)
        {
            
            
            DataTable dt = _db.ExecuteQuery(@"
            SELECT *
            FROM vw_POOutstanding
            WHERE POId = @POId AND OutstandingQty > 0",
                new[] {
                new SqlParameter("@POId", id)
                });

            //Query 2
            DataTable dtHeader = _db.ExecuteQuery(@"
            select PONumber from PurchaseOrders where POId=@POId",
            new[] {
                new SqlParameter("@POId", id)
            });

            // Ambil 1 row
            DataRow headerRow = null;
            if (dtHeader.Rows.Count > 0)
                headerRow = dtHeader.Rows[0];

            ViewBag.Header = headerRow;
            

            ViewBag.POId = id;
            return View(dt);
            
        }


        public IActionResult DetailPO(int id)
        {
            DataTable dt = _db.ExecuteQuery(@"
                                            SELECT 
                                            p.POId,
                                            p.PONumber,
                                            p.PODate,
                                            s.SupplierName,
                                            i.ItemName,
                                            pd.Qty,
                                            pd.Price,

                                            ISNULL(SUM(grd.Qty), 0) AS ReceivedQty,
                                            pd.Qty - ISNULL(SUM(grd.Qty), 0) AS OutstandingQty,

                                            CASE 
                                                WHEN pd.Qty <= ISNULL(SUM(grd.Qty), 0)
                                                    THEN 'CLOSED'
                                                ELSE 'OPEN'
                                            END AS Status

                                        FROM PurchaseOrders p
                                        INNER JOIN PurchaseOrderDetails pd ON pd.POId = p.POId
                                        INNER JOIN Suppliers s ON p.SupplierId = s.SupplierId
                                        INNER JOIN Items i ON pd.ItemId = i.ItemId
                                        LEFT JOIN GoodsReceiptDetails grd ON grd.PODetailId = pd.PODetailId

                                        WHERE p.POId = @POId

                                        GROUP BY
                                            p.POId, p.PONumber, p.PODate,
                                            s.SupplierName,
                                            i.ItemName,
                                            pd.PODetailId,
                                            pd.Qty,
                                            pd.Price;
",
             new[]
             {
                 new SqlParameter("@POId",id)
             });
            
            return View(dt);
        }

        public IActionResult PODetailAll(string keyword)
        {
            string query = @"
                            with p as (SELECT 
                            p.POId,
                            p.PONumber,
                            p.PODate,
                            s.SupplierName,
                            i.ItemName,
                            pd.Qty,
                            pd.Price,

                            ISNULL(SUM(grd.Qty), 0) AS ReceivedQty,
                            pd.Qty - ISNULL(SUM(grd.Qty), 0) AS OutstandingQty,

                            CASE 
                                WHEN pd.Qty <= ISNULL(SUM(grd.Qty), 0)
                                    THEN 'CLOSED'
                                ELSE 'OPEN'
                            END AS Status

                        FROM PurchaseOrders p
                        INNER JOIN PurchaseOrderDetails pd ON pd.POId = p.POId
                        INNER JOIN Suppliers s ON p.SupplierId = s.SupplierId
                        INNER JOIN Items i ON pd.ItemId = i.ItemId
                        LEFT JOIN GoodsReceiptDetails grd ON grd.PODetailId = pd.PODetailId

                        GROUP BY
                            p.POId, p.PONumber, p.PODate,
                            s.SupplierName,
                            i.ItemName,
                            pd.PODetailId,
                            pd.Qty,
                            pd.Price)
    
                            select p.POId,p.PONumber,p.PODate,p.SupplierName,p.ItemName,p.Qty,p.Price,p.ReceivedQty,p.OutstandingQty,p.Status from p where 1=1;

                            ";

            List<SqlParameter> parameters = new();

            if (!string.IsNullOrEmpty(keyword))
            {
                query += @" AND p.PONumber LIKE @PONumber ";
                parameters.Add(new SqlParameter("@PONumber", "%" + keyword + "%"));
            }

            DataTable dt = _db.ExecuteQuery(query, parameters.ToArray());

            ViewBag.Keyword = keyword;

            return View(dt);
        }


        public IActionResult OutstandingDetailAll(string keyword)
        {
            string query = @"
                WITH ar AS
                (
                    SELECT
                        d.PODetailId AS POItemId,
                        d.POId,
                        d.ItemId,
                        i.ItemName,
                        d.Qty AS POQty,
                        ISNULL(SUM(grd.Qty), 0) AS ReceivedQty,
                        d.Qty - ISNULL(SUM(grd.Qty), 0) AS OutstandingQty
                    FROM PurchaseOrderDetails d
                    JOIN Items i 
                        ON d.ItemId = i.ItemId
                    LEFT JOIN GoodsReceipts grh
                        ON grh.POId = d.POId
                    LEFT JOIN GoodsReceiptDetails grd
                        ON grd.ReceiptId = grh.ReceiptId
                       AND grd.ItemId = d.ItemId
                    GROUP BY
                        d.PODetailId,
                        d.POId,
                        d.ItemId,
                        i.ItemName,
                        d.Qty
                )
                SELECT
                    p.PONumber,
                    s.SupplierName,
                    p.PODate,
                    ar.POId,
                    ar.ItemId,
                    ar.ItemName,
                    ar.POQty,
                    ar.ReceivedQty,
                    ar.OutstandingQty
                FROM ar
                JOIN PurchaseOrders p
                    ON ar.POId = p.POId
                    JOIN Suppliers s on p.SupplierId = s.SupplierId
                WHERE ar.OutstandingQty > 0
                ";

            List<SqlParameter> parameters = new();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query += " AND p.PONumber LIKE @PONumber ";
                parameters.Add(new SqlParameter("@PONumber", "%" + keyword + "%"));
            }

            DataTable dt = _db.ExecuteQuery(query, parameters.ToArray());

            ViewBag.Keyword = keyword;

            return View(dt);
        }




    }
}
