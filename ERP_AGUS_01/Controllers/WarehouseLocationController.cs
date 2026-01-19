using ERP_AGUS_01.Data;
using ERP_AGUS_01.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ERP_AGUS_01.Controllers
{
    public class WarehouseLocationController : Controller
    {
        private readonly DbHelper _db;
        public WarehouseLocationController(DbHelper db) => _db = db;

        // ================= INDEX =================
        public IActionResult Index()
        {
            DataTable dt = _db.ExecuteQuery(@"
            SELECT l.LocationId, l.LocationCode,w.WarehouseName
            FROM WarehouseLocations l
            JOIN Warehouses w ON l.WarehouseId = w.WarehouseId

            ORDER BY l.LocationId DESC");

            List<WarehouseLocation> list = new();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new WarehouseLocation
                {
                    LocationId = (int)r["LocationId"],
                    LocationCode = r["LocationCode"].ToString(),
                    WarehouseName = r["WarehouseName"].ToString()
                });
            }
            return View(list);
        }

        // ================= CREATE =================
        public IActionResult Create()
        {
            ViewBag.Warehouses = _db.ExecuteQuery(
                "SELECT WarehouseId, WarehouseName FROM Warehouses order by WarehouseId asc");
            return View();
        }

        [HttpPost]
        public IActionResult Create(WarehouseLocation model)
        {
            _db.ExecuteNonQuery(
                "EXEC sp_InsertWarehouseLocation @WarehouseId",
                new[]
                {
                new SqlParameter("@WarehouseId", model.WarehouseId)
                });

            return RedirectToAction(nameof(Index));
        }

        // ================= EDIT =================
        public IActionResult Edit(int id)
        {
            ViewBag.Warehouses = _db.ExecuteQuery(
                "SELECT WarehouseId, WarehouseName FROM Warehouses order by WarehouseId ASC");

            DataTable dt = _db.ExecuteQuery(
                "SELECT * FROM WarehouseLocations WHERE LocationId=@Id",
                new[] { new SqlParameter("@Id", id) });

            if (dt.Rows.Count == 0)
                return NotFound();

            DataRow r = dt.Rows[0];
            return View(new WarehouseLocation
            {
                LocationId = (int)r["LocationId"],
                WarehouseId = (int)r["WarehouseId"],
                LocationCode = r["LocationCode"].ToString()
            });
        }

        [HttpPost]
        public IActionResult Edit(WarehouseLocation model)
        {
            _db.ExecuteNonQuery(
                @"UPDATE WarehouseLocations
              SET WarehouseId=@WarehouseId
              WHERE LocationId=@Id",
                new[]
                {
                new SqlParameter("@WarehouseId", model.WarehouseId),
                
                new SqlParameter("@Id", model.LocationId)
                });

            return RedirectToAction(nameof(Index));
        }

        // ================= DELETE (SOFT) =================
        public IActionResult Delete(int id)
        {
            _db.ExecuteNonQuery(
                "Delete WarehouseLocations WHERE LocationId=@Id",
                new[] { new SqlParameter("@Id", id) });

            return RedirectToAction(nameof(Index));
        }
    }
}
