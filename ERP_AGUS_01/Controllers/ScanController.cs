using Microsoft.AspNetCore.Mvc;

namespace ERP_AGUS_01.Controllers
{
    public class ScanController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Lookup(string barcode)
        {
            if (string.IsNullOrEmpty(barcode))
                return Json(new { found = false, message = "Barcode kosong" });

            string url = $"https://world.openfoodfacts.org/api/v0/product/{barcode}.json";

            using var http = new HttpClient();
            var response = await http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return Json(new { found = false, message = "Gagal akses API" });

            var json = await response.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }
    }
}
    
