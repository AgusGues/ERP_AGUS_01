using ERP_AGUS_01.Data;
using ERP_AGUS_01.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ERP_AGUS_01.Controllers
{
    public class SupplierController : Controller
    {
        private readonly DbHelper _db;
        public SupplierController(DbHelper db) => _db = db;

        // ================= INDEX =================
        public IActionResult Index()
        {
            DataTable dt = _db.ExecuteQuery(
                "SELECT * FROM Suppliers ORDER BY SupplierId DESC");

            List<Supplier> list = new();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new Supplier
                {
                    SupplierId = (int)r["SupplierId"],
                    SupplierCode = r["SupplierCode"].ToString(),
                    SupplierName = r["SupplierName"].ToString(),
                    Address = r["Address"]?.ToString(),
                    Phone = r["Phone"]?.ToString()
                });
            }
            return View(list);
        }

        // ================= CREATE =================
        public IActionResult Create() => View();

        [HttpPost]
        public IActionResult Create(Supplier model)
        {
            _db.ExecuteNonQuery(
                "EXEC sp_InsertSupplier @Name, @Address, @Phone",
                new[]
                {
                new SqlParameter("@Name", model.SupplierName),
                new SqlParameter("@Address", model.Address ?? ""),
                new SqlParameter("@Phone", model.Phone ?? "")
                });

            return RedirectToAction(nameof(Index));
        }

        // ================= EDIT =================
        public IActionResult Edit(int id)
        {
            DataTable dt = _db.ExecuteQuery(
                "SELECT * FROM Suppliers WHERE SupplierId=@Id",
                new[] { new SqlParameter("@Id", id) });

            if (dt.Rows.Count == 0)
                return NotFound();

            DataRow r = dt.Rows[0];
            return View(new Supplier
            {
                SupplierId = (int)r["SupplierId"],
                SupplierCode = r["SupplierCode"].ToString(),
                SupplierName = r["SupplierName"].ToString(),
                Address = r["Address"]?.ToString(),
                Phone = r["Phone"]?.ToString()
            });
        }

        [HttpPost]
        public IActionResult Edit(Supplier model)
        {
            _db.ExecuteNonQuery(
                @"UPDATE Suppliers
              SET SupplierName=@Name,
                  Address=@Address,
                  Phone=@Phone
              WHERE SupplierId=@Id",
                new[]
                {
                new SqlParameter("@Name", model.SupplierName),
                new SqlParameter("@Address", model.Address ?? ""),
                new SqlParameter("@Phone", model.Phone ?? ""),
                new SqlParameter("@Id", model.SupplierId)
                });

            return RedirectToAction(nameof(Index));
        }

        // ================= DELETE (SOFT) =================
        public IActionResult Delete(int id)
        {
            _db.ExecuteNonQuery(
                "Delete from Suppliers  WHERE SupplierId=@Id",
                new[] { new SqlParameter("@Id", id) });

            return RedirectToAction(nameof(Index));
        }
    }
}
