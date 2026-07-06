using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace RecordService.Controllers
{
    public class SourcesController : Controller
    {
        // GET: SourcesController
        public ActionResult Index()
        {
            return View();
        }

        // GET: SourcesController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: SourcesController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: SourcesController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: SourcesController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: SourcesController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: SourcesController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: SourcesController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
