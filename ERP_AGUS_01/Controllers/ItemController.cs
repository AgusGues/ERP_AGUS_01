using Microsoft.AspNetCore.Mvc;
using ERP_AGUS_01.Data;
using ERP_AGUS_01.Models;
using Microsoft.Data.SqlClient;

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

            return RedirectToAction("Index");
        }

    }
}
