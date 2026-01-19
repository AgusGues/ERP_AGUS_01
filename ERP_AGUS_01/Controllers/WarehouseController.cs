using ERP_AGUS_01.Data;
using ERP_AGUS_01.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ERP_AGUS_01.Controllers
{
    public class WarehouseController : Controller
    {
        private readonly DbHelper _db;
        public WarehouseController(DbHelper db) => _db = db;

        // ================= INDEX =================
        public IActionResult Index()
        {
            DataTable dt = _db.ExecuteQuery(
                "SELECT * FROM Warehouses ORDER BY WarehouseId DESC");

            List<Warehouses> list = new();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new Warehouses
                {
                    WarehouseId = (int)r["WarehouseId"],
                    WarehoseCode = r["WarehouseCode"].ToString(),
                    WarehouseName = r["WarehouseName"].ToString()
                });
            }
            return View(list);
        }

        // ================= CREATE =================
        public IActionResult Create() => View();

        [HttpPost]
        public IActionResult Create(Warehouses model)
        {
            _db.ExecuteNonQuery(
                "EXEC sp_InsertWarehouse @Name",
                new[]
                {
                new SqlParameter("@Name", model.WarehouseName)
                });

            return RedirectToAction(nameof(Index));
        }

        // ================= EDIT =================
        public IActionResult Edit(int id)
        {
            DataTable dt = _db.ExecuteQuery(
                "SELECT * FROM Warehouses WHERE WarehouseId=@Id",
                new[] { new SqlParameter("@Id", id) });

            if (dt.Rows.Count == 0)
                return NotFound();

            DataRow r = dt.Rows[0];
            return View(new Warehouses
            {
                WarehouseId = (int)r["WarehouseId"],
                WarehoseCode = r["WarehouseCode"].ToString(),
                WarehouseName = r["WarehouseName"].ToString()
            });
        }

        [HttpPost]
        public IActionResult Edit(Warehouses model)
        {
            _db.ExecuteNonQuery(
                @"UPDATE Warehouses
              SET WarehouseName=@Name
              WHERE WarehouseId=@Id",
                new[]
                {
                new SqlParameter("@Name", model.WarehouseName),
                new SqlParameter("@Id", model.WarehouseId)
                });

            return RedirectToAction(nameof(Index));
        }

        // ================= DELETE (SOFT) =================
        public IActionResult Delete(int id)
        {
            _db.ExecuteNonQuery(
                "delete Warehouses  WHERE WarehouseId=@Id",
                new[] { new SqlParameter("@Id", id) });

            return RedirectToAction(nameof(Index));
        }
    }
}
