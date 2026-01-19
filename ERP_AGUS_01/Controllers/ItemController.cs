using Microsoft.AspNetCore.Mvc;
using ERP_AGUS_01.Data;
using ERP_AGUS_01.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ERP_AGUS_01.Controllers
{
    public class ItemController : Controller
    {
        private readonly DbHelper _db;
        public ItemController(DbHelper db) => _db = db;

        public IActionResult Index()
        {
            var dt = _db.ExecuteQuery("select ItemId,ItemName,ItemCode, Unit from Items");
            List<Items> list = new();
            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(new Items
                {
                    ItemId = (int)row["ItemId"],
                    ItemCode = row["ItemCode"].ToString(),
                    ItemName = row["ItemName"].ToString(),
                    Unit = row["Unit"].ToString()
                });
            }

            return View(list);

           
           
        }

        public IActionResult Create() => View();
        [HttpPost]
        public IActionResult Create(Items model)
        {
            _db.ExecuteNonQuery(
                "EXEC sp_InsertItem @Name, @Unit",
                new[]
                {
            new SqlParameter("@Name", model.ItemName),
            new SqlParameter("@Unit", model.Unit)
                });

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Edit(int id)
        {
            DataTable dt = _db.ExecuteQuery
                (
                "select * from items where Itemid=@Id",
                new[] {
                new SqlParameter("@Id",id)
                });

            if (dt.Rows.Count == 0)
                return NotFound();
            
            DataRow r = dt.Rows[0];
            return View(new Items
            {
                ItemId = (int)r["ItemId"],
                ItemCode = r["ItemCode"].ToString(),
                ItemName = r["ItemName"].ToString(),
                Unit = r["Unit"].ToString()
            });

        }

        [HttpPost]
        public IActionResult Edit(Items model)
        {
            _db.ExecuteNonQuery(
                @"UPDATE Items
                  SET 
                     ItemName=@Name,
                     Unit=@Unit
                  WHERE ItemId=@Id",
new[]
{
    new SqlParameter("@Name", model.ItemName),
    new SqlParameter("@Unit", model.Unit),
    new SqlParameter("@Id", model.ItemId)
});

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(int id)
        {
            // Cek apakah item sudah dipakai
            int used = Convert.ToInt32(
                _db.ExecuteScalar(@"
                SELECT COUNT(*) FROM (
                    SELECT ItemId FROM PurchaseOrderDetails WHERE ItemId=@Id
                    UNION ALL
                    SELECT ItemId FROM GoodsReceiptDetails WHERE ItemId=@Id
                    UNION ALL
                    SELECT ComponentItemId ItemId FROM BOMDetails WHERE ComponentItemId=@Id
                    UNION ALL
                    SELECT ItemId FROM SalesDetails WHERE ItemId=@Id
                ) x",
                    new[] { new SqlParameter("@Id", id) }
                ));

            if (used > 0)
            {
                TempData["Error"] = "Item sudah dipakai, tidak bisa dihapus!";
                return RedirectToAction(nameof(Index));
            }

            _db.ExecuteNonQuery(
                "DELETE FROM Items WHERE ItemId=@Id",
                new[] { new SqlParameter("@Id", id) });

            return RedirectToAction(nameof(Index));
        }
    }

}

