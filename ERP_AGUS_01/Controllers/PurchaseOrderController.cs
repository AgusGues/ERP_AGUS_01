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
            WHERE POId = @POId
              AND OutstandingQty > 0",
                new[] {
                new SqlParameter("@POId", id)
                });

            ViewBag.POId = id;
            return View(dt);
        }



    }
}
