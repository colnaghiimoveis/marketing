using Microsoft.AspNetCore.Mvc;
using ColnaghiMarketingTools.Models;
using ColnaghiMarketingTools.Repositories;

namespace ColnaghiMarketingTools.ViewComponents
{
    public class FolderMenuViewComponent : ViewComponent
    {
        private readonly FolderRepository _folderRepository;

        public FolderMenuViewComponent(IConfiguration config)
        {
            var connectionString = config.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            _folderRepository = new FolderRepository(connectionString);
        }

        public IViewComponentResult Invoke()
        {
            try
            {
                var pastas = _folderRepository.Listar();
                return View(pastas);
            }
            catch
            {
                // Em caso de erro, retorna uma lista vazia
                return View(new List<Pasta>());
            }
        }
    }
} 