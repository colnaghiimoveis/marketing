using ColnaghiMarketingTools.Models;
using ColnaghiMarketingTools.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace ColnaghiMarketingTools.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly FolderRepository _folderRepository;
        private readonly CampanhaRepository _campanhaRepository;
        private readonly CampanhaImovelRepository _campanhaImovelRepository;

        public HomeController(ILogger<HomeController> logger, IConfiguration config)
        {
            _logger = logger;
            var connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            _folderRepository = new FolderRepository(connectionString);
            _campanhaRepository = new CampanhaRepository(connectionString);
            _campanhaImovelRepository = new CampanhaImovelRepository(connectionString);
        }

        public IActionResult Index()
        {
            var pastas = _folderRepository.Listar();
            ViewBag.UsuarioNome = HttpContext.Session.GetString("UsuarioNome") ?? "Usuário";
            return View(pastas);
        }

        [HttpPost]
        public IActionResult SalvarRascunho(NovaCampanhaViewModel model, string acao)
        {
            if (!ModelState.IsValid)
            {
                var pastas = _folderRepository.Listar();
                return View("Index", pastas);
            }

            try
            {
                // Verifica se já existe rascunho igual
                var rascunhoExistente = _campanhaRepository.ListarRascunhos()
                    .FirstOrDefault(c => c.Nome == model.Nome && c.PastaId == model.PastaId && c.TipoTemplate == model.TipoTemplate);

                long campanhaId;
                if (rascunhoExistente != null)
                {
                    // Atualiza o rascunho existente
                    rascunhoExistente.Nome = model.Nome;
                    rascunhoExistente.PastaId = model.PastaId;
                    rascunhoExistente.TipoTemplate = model.TipoTemplate;
                    _campanhaRepository.AtualizarCampanha(rascunhoExistente);
                    campanhaId = rascunhoExistente.Id;
                }
                else
                {
                    var campanha = new Campanha
                    {
                        Nome = model.Nome,
                        PastaId = model.PastaId,
                        TipoTemplate = model.TipoTemplate
                    };
                    campanhaId = _campanhaRepository.SalvarRascunho(campanha);
                }

                if (acao == "rascunho")
                {
                    TempData["SuccessMessage"] = "Rascunho salvo com sucesso!";
                    var pastas = _folderRepository.Listar();
                    return View("Index", pastas);
                }
                else // avancar
                {
                    return RedirectToAction("EditarCampanha", new { id = campanhaId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar rascunho da campanha");
                ModelState.AddModelError("", "Erro ao salvar a campanha. Tente novamente.");
                var pastas = _folderRepository.Listar();
                return View("Index", pastas);
            }
        }

        public IActionResult EditarCampanha(long id)
        {
            var campanha = _campanhaRepository.ObterPorId(id);
            if (campanha == null)
            {
                return NotFound();
            }

            return View(campanha);
        }

        [HttpPost]
        public async Task<IActionResult> AtualizarCampanha(Campanha model)
        {
            try
            {
                _campanhaRepository.AtualizarCampanha(model);
                // Salvar imóveis
                _campanhaImovelRepository.RemoverPorCampanha(model.Id);
                if (model.Imoveis != null && model.Imoveis.Any(i => !string.IsNullOrWhiteSpace(i.CodigoImovel)))
                {
                    var imoveisValidos = new List<CampanhaImovel>();
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.Accept.Clear();
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
                        foreach (var imovel in model.Imoveis)
                        {
                            if (string.IsNullOrWhiteSpace(imovel.CodigoImovel)) continue;
                            var url = $"https://colnaghi-rest.vistahost.com.br/imoveis/detalhes?key=8b0dc65f996f98fd178a9defd0efa077&showSuspended=1&showInternal=1&imovel={imovel.CodigoImovel}&pesquisa=%7B%22fields%22%3A[%22Codigo%22,%22ValorVenda%22,%20%22Dormitorios%22,%20%22Vagas%22,%22AreaPrivativa%22,%22PrecoEspecial%22,%22Promocao%22,%22Categoria%22,%22Bairro%22,%22FotoDestaquePequena%22]%7D";
                            var response = await httpClient.GetAsync(url);
                            if (response.IsSuccessStatusCode)
                            {
                                var json = await response.Content.ReadAsStringAsync();
                                var doc = JsonDocument.Parse(json);
                                var root = doc.RootElement;
                                imovel.ValorVenda = decimal.Parse(root.GetPropertyOrNull("ValorVenda")?.GetString());
                                imovel.Dormitorios = int.Parse( root.GetPropertyOrNull("Dormitorios")?.GetString());
                                imovel.Vagas = int.Parse(root.GetPropertyOrNull("Vagas")?.GetString());
                                var areaPrivativaStr = root.GetPropertyOrNull("AreaPrivativa")?.GetString();
                                if (!string.IsNullOrEmpty(areaPrivativaStr))
                                {
                                    areaPrivativaStr = areaPrivativaStr.Replace('.', ',');
                                    imovel.AreaPrivativa = decimal.Parse(areaPrivativaStr);
                                }

                                var valorespecial = root.GetPropertyOrNull("PrecoEspecial")?.GetString();

                                imovel.ValorVendaEspecial = decimal.Parse(valorespecial == "" ? "0" : valorespecial);
                                imovel.Promocao = root.GetPropertyOrNull("Promocao")?.GetString();
                                imovel.Tipo = root.GetPropertyOrNull("Categoria")?.GetString();
                                imovel.Bairro = root.GetPropertyOrNull("Bairro")?.GetString();
                                imovel.FotoDestaquePequena = root.GetPropertyOrNull("FotoDestaquePequena")?.GetString();
                                string RemoverAcentos(string texto)
                                {
                                    if (string.IsNullOrEmpty(texto)) return "";
                                    var normalized = texto.Normalize(System.Text.NormalizationForm.FormD);
                                    var sb = new System.Text.StringBuilder();
                                    foreach (var c in normalized)
                                    {
                                        if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                                            sb.Append(c);
                                    }
                                    return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
                                }
                                string Slug(string texto)
                                {
                                    return RemoverAcentos(texto).ToLower().Replace(" ", "-").Replace("--", "-");
                                }
                                var tipoSlug = Slug(imovel.Tipo ?? "");
                                var bairroSlug = Slug(imovel.Bairro ?? "");
                                var cidadeSlug = "porto-alegre";

                                imovel.UrlLink = $"https://www.colnaghi.com.br/imovel/{tipoSlug}-{bairroSlug}-{imovel.Dormitorios}-dorms-{cidadeSlug}-venda-{imovel.CodigoImovel}";
                                imoveisValidos.Add(imovel);
                            }
                        }
                    }
                    if (imoveisValidos.Any())
                        _campanhaImovelRepository.InserirImoveis(model.Id, imoveisValidos);
                }
                TempData["SuccessMessage"] = "Campanha atualizada com sucesso!";
                return RedirectToAction("VisualizarCampanha", new { id = model.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar campanha");
                ModelState.AddModelError("", "Erro ao atualizar a campanha. Tente novamente.");
                var campanha = _campanhaRepository.ObterPorId(model.Id);
                if (campanha != null)
                {
                    campanha.Titulo = model.Titulo;
                    campanha.Chamada = model.Chamada;
                    campanha.CorpoTexto = model.CorpoTexto;
                    campanha.Rodape = model.Rodape;
                }
                return View("EditarCampanha", campanha);
            }
        }

        public IActionResult VisualizarCampanha(long id)
        {
            var campanha = _campanhaRepository.ObterPorId(id);
            if (campanha == null)
            {
                return NotFound();
            }
            return View(campanha);
        }

        [HttpPost]
        public IActionResult GerarEmailMarketing(long id)
        {
            var campanha = _campanhaRepository.ObterPorId(id);
            if (campanha == null)
                return NotFound();

            // CSS completo do site para máxima fidelidade
            var css = @"REPLACE_CSS_HERE";

            var html = $@"<!DOCTYPE html>
                            <html lang=""pt-br"">
                            <head>
                                <meta charset=""UTF-8"">
                                <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
                                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                                <title>{campanha.Titulo}</title>
                                <style>{css}</style>
                            </head>
                            <body>
                                <div class=""main-content-wrapper"">
                                    <div class=""preview-final-container"">
                                        <div class=""preview-final-email"">
                                            <div class=""preview-logo"">
                                                <img src=""https://www.colnaghi.com.br/assets/images/logotipo.png"" alt=""Colnaghi Logo"" />
                                            </div>
                                            <hr class=""preview-divider"" />
                                            <div class=""preview-title"">
                                                <h1>{campanha.Titulo}</h1>
                                            </div>
                                            <div class=""preview-chamada"">
                                                <p>{campanha.Chamada}</p>
                                            </div>
                                            <div class=""preview-cta"">
                                                <a href=""#"" class=""btn-cta-vermelho"">Conheça o Lançamento</a>
                                            </div>
                                            <div class=""preview-corpo"">
                                                <p>{campanha.CorpoTexto}</p>
                                            </div>
                                            <h3 class=""unidades-titulo-vermelho"">Unidades</h3>
                                            <div class=""preview-unidades"">
                                                <hr class=""unidades-divider"" />
                                                <div class=""unidades-lista-grid {(campanha.TipoExibicao == 1 ? "um-por-linha" : "dois-por-linha")}"">
                                                    {string.Join("", campanha.Imoveis.Select(imovel => $@"
                                                    <a href='{imovel.UrlLink}' target='_blank' style='text-decoration:none;color:inherit;'>
                                                        <div class='unidade-card-figma'>
                                                            <div class='unidade-foto-container'>
                                                                {(string.IsNullOrEmpty(imovel.FotoDestaquePequena) ? "" : $"<img src='{imovel.FotoDestaquePequena}' alt='Foto Imóvel' class='unidade-foto-figma' />")}
                                                                <div class='unidade-codigo-sobre-imagem'>{imovel.CodigoImovel}</div>
                                                                {(string.IsNullOrEmpty(imovel.Promocao) ? "" : $"<div class='unidade-promocao-sobre-imagem'>{imovel.Promocao}</div>")}
                                                            </div>
                                                            <div class='unidade-bairro-tipo-figma'>{(string.IsNullOrEmpty(imovel.Tipo) ? "" : char.ToUpper(imovel.Tipo[0]) + imovel.Tipo.Substring(1).ToLower())} no {(string.IsNullOrEmpty(imovel.Bairro) ? "" : char.ToUpper(imovel.Bairro[0]) + imovel.Bairro.Substring(1).ToLower())}</div>
                                                            <div class='unidade-detalhes-valor-container'>
                                                                <div class='unidade-detalhes-figma'>
                                                                    <span><img src='https://www.colnaghi.com.br/img/icone_tamanho.jpg' /> {imovel.AreaPrivativa} m²</span>
                                                                    <span class='detalhe-sep'>|</span>
                                                                    <span><img src='https://www.colnaghi.com.br/img/icone_carro.jpg' /> {imovel.Vagas}</span>
                                                                    <span class='detalhe-sep'>|</span>
                                                                    <span><img src='https://www.colnaghi.com.br/img/icone_dormitorio.jpg' /> {imovel.Dormitorios}</span>
                                                                </div>
                                                                <div class='unidade-valor-figma'>{(imovel.ValorVenda.HasValue ? ((int)imovel.ValorVenda.Value).ToString("C0") : "")}</div>
                                                            </div>
                                                        </div>
                                                    </a>
                                                    "))}
                                                </div>
                                            </div>
                                            <div class=""preview-rodape"">
                                                <p>{campanha.Rodape}</p>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </body>
                            </html>";

            // Substituir o placeholder pelo CSS real
            html = html.Replace("REPLACE_CSS_HERE", System.IO.File.ReadAllText("wwwroot/css/site.css"));

            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            var fileName = $"campanha_{id}_email.html";
            return File(bytes, "text/html", fileName);
        }

        public IActionResult Rascunhos()
        {
            var campanhas = _campanhaRepository.ListarRascunhos();
            return View(campanhas);
        }

        public IActionResult Historico()
        {
            var campanhas = _campanhaRepository.ListarHistorico(10);
            return View(campanhas);
        }

        public IActionResult CampanhasPorPasta(long pastaId)
        {
            var campanhas = _campanhaRepository.ListarPorPasta(pastaId);
            ViewBag.PastaId = pastaId;
            return View(campanhas);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public IActionResult DeletarCampanha(long id, string? returnUrl = null)
        {
            _campanhaRepository.Deletar(id);
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Rascunhos");
        }
    }
}

// Métodos auxiliares para JSON
public static class JsonElementExtensions
{
    public static JsonElement? GetPropertyOrNull(this JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
            return prop;
        return null;
    }
    public static decimal? GetDecimal(this JsonElement? element)
    {
        if (element == null) return null;
        if (element.Value.ValueKind == JsonValueKind.String && decimal.TryParse(element.Value.GetString(), out var d)) return d;
        if (element.Value.ValueKind == JsonValueKind.Number) return element.Value.GetDecimal();
        return null;
    }
    public static int? GetInt32(this JsonElement? element)
    {
        if (element == null) return null;
        if (element.Value.ValueKind == JsonValueKind.String && int.TryParse(element.Value.GetString(), out var i)) return i;
        if (element.Value.ValueKind == JsonValueKind.Number) return element.Value.GetInt32();
        return null;
    }
    public static string? GetString(this JsonElement? element)
    {
        if (element == null) return null;
        return element.Value.GetString();
    }
}
