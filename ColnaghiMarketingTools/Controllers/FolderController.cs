using Microsoft.AspNetCore.Mvc;
using ColnaghiMarketingTools.Models;
using ColnaghiMarketingTools.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace ColnaghiMarketingTools.Controllers
{
    [Authorize]
    public class FolderController : Controller
    {
        private readonly FolderRepository _repo;

        public FolderController(IConfiguration config)
        {
            var connectionString = config.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            _repo = new FolderRepository(connectionString);
        }

        public IActionResult Index()
        {
            var pastas = _repo.Listar();
            return View(pastas);
        }

        [HttpPost]
        public IActionResult Adicionar(string nome)
        {
            if (!string.IsNullOrWhiteSpace(nome))
                _repo.Adicionar(nome);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Excluir(long id)
        {
            _repo.Excluir(id);
            return RedirectToAction("Index");
        }
    }
} 