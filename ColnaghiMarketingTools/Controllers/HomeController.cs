using ColnaghiMarketingTools.Models;
using ColnaghiMarketingTools.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Globalization;
using System.Linq; // <- necessário para FirstOrDefault/Any
using System.Text;

namespace ColnaghiMarketingTools.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly TextInfo PtBrTextInfo = PtBrCulture.TextInfo;
        private const string VistaDetalhesUrl = "https://colnaghi-rest.vistahost.com.br/imoveis/detalhes?key=8b0dc65f996f98fd178a9defd0efa077&showSuspended=1&showInternal=1&imovel={0}&pesquisa=%7B%22fields%22%3A[%22Codigo%22,%22ValorVenda%22,%20%22Dormitorios%22,%20%22Vagas%22,%22AreaPrivativa%22,%22PrecoEspecial%22,%22Promocao%22,%22Categoria%22,%22Bairro%22,%22FotoDestaquePequena%22]%7D";

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
                    // Captura o texto acima do logo (não persiste no banco)
                    var headerTexto = (Request.Form["HeaderTexto"].ToString() ?? "").Trim();
                    TempData["HeaderTexto"] = headerTexto;

                    // Captura o título editável da seção Unidades (não persiste no banco)
                    var tituloUnidades = (Request.Form["TituloUnidades"].ToString() ?? "").Trim();
                    TempData["TituloUnidades"] = string.IsNullOrWhiteSpace(tituloUnidades) ? "Unidades" : tituloUnidades;

                    // CTA secundário (não persiste)
                    var ctaSecTexto = (Request.Form["CtaSecundarioTexto"].ToString() ?? "").Trim();
                    var ctaSecUrl = (Request.Form["CtaSecundarioUrl"].ToString() ?? "").Trim();
                    TempData["CtaSecundarioTexto"] = ctaSecTexto;
                    TempData["CtaSecundarioUrl"] = ctaSecUrl;

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

            ViewBag.HeaderTexto = TempData.Peek("HeaderTexto") as string ?? string.Empty;
            ViewBag.TituloUnidades = TempData.Peek("TituloUnidades") as string ?? "Unidades";
            ViewBag.CtaSecundarioTexto = TempData.Peek("CtaSecundarioTexto") as string ?? string.Empty;
            ViewBag.CtaSecundarioUrl = TempData.Peek("CtaSecundarioUrl") as string ?? string.Empty;

            var customTitulosJson = TempData.Peek("ImoveisCustomTitulos") as string;
            ViewBag.ImoveisCustomTitulos = ParseCustomTitles(customTitulosJson);
            ViewBag.ImoveisCustomTitulosJson = customTitulosJson ?? string.Empty;

            TempData.Keep("HeaderTexto");
            TempData.Keep("TituloUnidades");
            TempData.Keep("CtaSecundarioTexto");
            TempData.Keep("CtaSecundarioUrl");
            TempData.Keep("ImoveisCustomTitulos");

            return View(campanha);
        }

        [HttpPost]
        public async Task<IActionResult> AtualizarCampanha(Campanha model)
        {
            try
            {
                _campanhaRepository.AtualizarCampanha(model);

                // Captura títulos personalizados (não persistem em banco, apenas na sessão atual)
                var customTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (model.Imoveis != null)
                {
                    for (int index = 0; index < model.Imoveis.Count; index++)
                    {
                        var key = $"ImoveisCustomTitulo[{index}]";
                        if (Request.Form.TryGetValue(key, out var tituloValor))
                        {
                            var titulo = tituloValor.ToString().Trim();
                            if (!string.IsNullOrWhiteSpace(titulo))
                            {
                                customTitles[$"idx{index}"] = titulo;
                            }
                        }
                    }
                }

                // Salvar imóveis
                _campanhaImovelRepository.RemoverPorCampanha(model.Id);
                if (model.Imoveis != null && model.Imoveis.Any(i => !string.IsNullOrWhiteSpace(i.CodigoImovel)))
                {
                    var imoveisValidos = new List<CampanhaImovel>();

                    foreach (var imovel in model.Imoveis)
                    {
                        var codigo = imovel?.CodigoImovel?.Trim();
                        if (string.IsNullOrWhiteSpace(codigo))
                        {
                            continue;
                        }

                        var detalhes = await BuscarImovelNaApiAsync(codigo);
                        if (detalhes == null)
                        {
                            continue;
                        }

                        detalhes.CampanhaId = model.Id;
                        imoveisValidos.Add(detalhes);
                    }

                    if (imoveisValidos.Any())
                    {
                        _campanhaImovelRepository.InserirImoveis(model.Id, imoveisValidos);
                    }
                }

                // Captura campos não persistentes (HeaderTexto e TituloUnidades)
                var headerTexto = (Request.Form["HeaderTexto"].ToString() ?? "").Trim();
                TempData["HeaderTexto"] = headerTexto;

                var tituloUnidades = (Request.Form["TituloUnidades"].ToString() ?? "").Trim();
                TempData["TituloUnidades"] = string.IsNullOrWhiteSpace(tituloUnidades) ? "Unidades" : tituloUnidades;

                var ctaSecundarioTexto = (Request.Form["CtaSecundarioTexto"].ToString() ?? "").Trim();
                var ctaSecundarioUrl = (Request.Form["CtaSecundarioUrl"].ToString() ?? "").Trim();
                TempData["CtaSecundarioTexto"] = ctaSecundarioTexto;
                TempData["CtaSecundarioUrl"] = ctaSecundarioUrl;

                if (customTitles.Any())
                {
                    TempData["ImoveisCustomTitulos"] = JsonSerializer.Serialize(customTitles);
                }
                else
                {
                    TempData.Remove("ImoveisCustomTitulos");
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

            // Carrega o texto acima do logo do fluxo (sem persistir em banco)
            var headerTexto = TempData["HeaderTexto"] as string ?? "";
            ViewBag.HeaderTexto = headerTexto;

            var tituloUnidades = TempData["TituloUnidades"] as string ?? "Unidades";
            ViewBag.TituloUnidades = tituloUnidades;

            var ctaSecundarioTexto = TempData["CtaSecundarioTexto"] as string ?? string.Empty;
            var ctaSecundarioUrl = TempData["CtaSecundarioUrl"] as string ?? string.Empty;
            ViewBag.CtaSecundarioTexto = ctaSecundarioTexto;
            ViewBag.CtaSecundarioUrl = ctaSecundarioUrl;

            var customTitulosJson = TempData["ImoveisCustomTitulos"] as string;
            var customTitulos = ParseCustomTitles(customTitulosJson);
            ViewBag.ImoveisCustomTitulos = customTitulos;
            ViewBag.ImoveisCustomTitulosJson = customTitulosJson ?? string.Empty;

            TempData.Keep("HeaderTexto");
            TempData.Keep("TituloUnidades");
            TempData.Keep("CtaSecundarioTexto");
            TempData.Keep("CtaSecundarioUrl");
            TempData.Keep("ImoveisCustomTitulos");

            return View(campanha);
        }

        [HttpPost]
        public IActionResult GerarEmailMarketing(long id)
        {
            var campanha = _campanhaRepository.ObterPorId(id);
            if (campanha == null) return NotFound();
            
            var customTitulosJson = TempData["ImoveisCustomTitulos"] as string;
            if (string.IsNullOrWhiteSpace(customTitulosJson))
            {
                customTitulosJson = Request.Form["ImoveisCustomTitulosJson"].ToString();
            }
            var customTitulos = ParseCustomTitles(customTitulosJson);


            // ----- Texto acima do logo (preferir TempData; fallback POST) -----
            var headerTexto = (TempData["HeaderTexto"] as string ?? Request.Form["HeaderTexto"].ToString() ?? "").Trim();
            var headerHtml = string.IsNullOrWhiteSpace(headerTexto)
                ? ""
                : $@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:12px 0;"">
                        <tr>
                          <td align=""center"" style=""font-family:Arial,Helvetica,sans-serif;color:#222222;font-size:14px;line-height:1.4;padding:6px 12px;"">
                            {System.Net.WebUtility.HtmlEncode(headerTexto)}
                          </td>
                        </tr>
                      </table>";

            // ----- Título da seção Unidades (não persistido em banco) -----
            var tituloUnidades = (TempData["TituloUnidades"] as string ?? Request.Form["TituloUnidades"].ToString() ?? "Unidades").Trim();
            if (string.IsNullOrWhiteSpace(tituloUnidades)) tituloUnidades = "Unidades";

            var ctaSecundarioTexto = (TempData["CtaSecundarioTexto"] as string ?? Request.Form["CtaSecundarioTexto"].ToString() ?? "").Trim();
            var ctaSecundarioUrl = (TempData["CtaSecundarioUrl"] as string ?? Request.Form["CtaSecundarioUrl"].ToString() ?? "").Trim();

            // ----- Normalizações do CTA -----
            string BuildCtaHtml(string? texto, string? url)
            {
                var normalizedTexto = (texto ?? string.Empty).Trim();
                var normalizedUrl = (url ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(normalizedTexto) || string.IsNullOrWhiteSpace(normalizedUrl))
                {
                    return string.Empty;
                }

                if (!(normalizedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                      normalizedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    normalizedUrl = "https://" + normalizedUrl;
                }

                var btnBg = "#E53935";
                var btnColor = "#FFFFFF";
                var btnRadius = 6;
                var btnPaddingV = 12;
                var btnPaddingH = 24;
                var btnFont = "Arial, Helvetica, sans-serif";
                var btnFontSize = 16;

                return
$@"<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin:16px 0;"">
  <tr>
    <td align=""center"">
      <!--[if mso]>
      <v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word""
        href=""{normalizedUrl}"" style=""height:{btnPaddingV * 2 + 22}px;v-text-anchor:middle;width:260px;"" arcsize=""{btnRadius * 100 / 200}%""
        fillcolor=""{btnBg}"" stroked=""f"">
        <w:anchorlock/>
        <center style=""color:{btnColor};font-family:{btnFont};font-size:{btnFontSize}px;font-weight:bold;"">
          {System.Net.WebUtility.HtmlEncode(normalizedTexto)}
        </center>
      </v:roundrect>
      <![endif]-->
      <!--[if !mso]><!-- -->
      <a href=""{normalizedUrl}"" target=""_blank"" rel=""noopener""
         style=""display:inline-block;background:{btnBg};color:{btnColor};text-decoration:none;font-family:{btnFont};
                font-weight:bold;font-size:{btnFontSize}px;line-height:1;border-radius:{btnRadius}px;
                padding:{btnPaddingV}px {btnPaddingH}px;"">
        {System.Net.WebUtility.HtmlEncode(normalizedTexto)}
      </a>
      <!--<![endif]-->
    </td>
  </tr>
</table>";
            }

            var ctaHtmlTopo = BuildCtaHtml(campanha.CtaTexto, campanha.CtaUrl);
            var ctaHtmlRodape = BuildCtaHtml(ctaSecundarioTexto, ctaSecundarioUrl);

            // ----- Montagem das UNIDADES em 2 colunas -----
            var imoveis = campanha.Imoveis ?? new List<CampanhaImovel>();
            string RenderUnidade(CampanhaImovel im, int index)
            {
                var foto = im.FotoDestaquePequena ?? "";
                var url = im.UrlLink ?? "#";
                int? areaInteira = im.AreaPrivativa.HasValue
                    ? (int)Math.Round(im.AreaPrivativa.Value, MidpointRounding.AwayFromZero)
                    : (int?)null;
                var area = areaInteira.HasValue ? $"{areaInteira.Value} m²" : string.Empty;
                var vagas = im.Vagas.HasValue ? im.Vagas.Value.ToString() : string.Empty;
                var dorms = im.Dormitorios.HasValue ? im.Dormitorios.Value.ToString() : string.Empty;
                var codigoUpper = string.IsNullOrWhiteSpace(im.CodigoImovel) ? string.Empty : im.CodigoImovel.ToUpperInvariant();
                var valor = im.ValorVenda.HasValue ? $"R$ {((int)im.ValorVenda.Value).ToString("N0", PtBrCulture)}" : string.Empty;
                var tipo = (im.Tipo ?? string.Empty).Trim();
                var bairro = (im.Bairro ?? string.Empty).Trim();
                string bairroTipo;
                if (string.IsNullOrEmpty(tipo) && string.IsNullOrEmpty(bairro))
                {
                    bairroTipo = string.Empty;
                }
                else if (string.IsNullOrEmpty(tipo))
                {
                    bairroTipo = bairro;
                }
                else if (string.IsNullOrEmpty(bairro))
                {
                    bairroTipo = tipo;
                }
                else
                {
                    bairroTipo = $"{tipo} no {bairro}";
                }

                if (customTitulos.TryGetValue($"idx{index}", out var customTitulo) &&
                    !string.IsNullOrWhiteSpace(customTitulo))
                {
                    bairroTipo = customTitulo.Trim();
                }

                var detalhesParts = new List<string>();
                if (!string.IsNullOrEmpty(area))
                {
                    detalhesParts.Add($"<span style=\"display:inline-block;margin:0 6px;font-size:12px\"><img src='http://marketing.colnaghisistemas.kinghost.net/img/icone_tamanho.jpg' alt='' style='vertical-align:middle;border:none;outline:none;width:14px;height:14px;'> {System.Net.WebUtility.HtmlEncode(area)}</span>");
                }
                if (!string.IsNullOrEmpty(dorms))
                {
                    detalhesParts.Add($"<span style=\"display:inline-block;margin:0 6px;font-size:12px\"><img src='http://marketing.colnaghisistemas.kinghost.net/img/icone_dormitorio.jpg' alt='' style='vertical-align:middle;border:none;outline:none;width:14px;height:14px;'> {System.Net.WebUtility.HtmlEncode(dorms)}</span>");
                }
                if (!string.IsNullOrEmpty(vagas))
                {
                    detalhesParts.Add($"<span style=\"display:inline-block;margin:0 6px;font-size:12px\"><img src='http://marketing.colnaghisistemas.kinghost.net/img/icone_carro.jpg' alt='' style='vertical-align:middle;border:none;outline:none;width:14px;height:14px;'> {System.Net.WebUtility.HtmlEncode(vagas)}</span>");
                }
                var detalhesInline = detalhesParts.Count > 0
                    ? string.Join("<span style=\"display:inline-block;margin:0 6px;color:#BBBBBB;\">|</span>", detalhesParts)
                    : string.Empty;

                // Card da unidade com estilo inline + imagem fluida
                return
$@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border-collapse:separate;border-spacing:0;background:#FFFFFF;border-radius:16px;border:1px solid #EEEEEE;overflow:hidden;padding:0;"">
  <tr>
    <td style=""padding:0 0 12px 0;"">
      <a href=""{url}"" target=""_blank"" style=""text-decoration:none;color:#000000;"">
        {(string.IsNullOrEmpty(foto) ? "" :
        $@"<img src=""{foto}"" alt=""{System.Net.WebUtility.HtmlEncode(bairroTipo)}"" width=""100%"" style=""display:block;border:1px solid #EEEEEE;border-radius:15px;width:100%;height:auto;min-height:175px;background-color:#F8F8F8;"" />")}
      </a>
    </td>
  </tr>
  {(string.IsNullOrWhiteSpace(codigoUpper) ? "" :
    $@"<tr>
        <td style=""padding:0 0 8px 0;color:#888888;font-family:Arial,Helvetica,sans-serif;font-size:14px;text-align:center;"">
          {System.Net.WebUtility.HtmlEncode(codigoUpper)}
        </td>
      </tr>")}
  <tr>
    <td style=""padding:0 0 8px 0;color:#000000;font-family:Arial,Helvetica,sans-serif;font-size:16px;font-weight:bold;text-align:center;border-bottom:1px solid #E0E0E0;"">
      {System.Net.WebUtility.HtmlEncode(bairroTipo)}
    </td>
  </tr>
  <tr>
    <td style=""padding:16px 0 0 0; font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#000000;text-align:center;"">
      {detalhesInline}
      <div style=""width:100%; margin-top:20px;text-align:center;color:#222222;font-weight:bold;font-size:18px"">{valor}</div><br>
    </td>
  </tr>
</table>";
            }

            // Render em linhas de 2 colunas
            var unidadesHtml = "";
            for (int i = 0; i < imoveis.Count; i += 2)
            {
                var left = RenderUnidade(imoveis[i], i);
                var right = (i + 1 < imoveis.Count) ? RenderUnidade(imoveis[i + 1], i + 1) : "";

                unidadesHtml +=
$@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin-bottom:24px;"">
    <tr>
      <td valign=""top"" width=""50%"" style=""padding-right:12px;"">{left}</td>
      <td valign=""top"" width=""50%"" style=""padding-left:12px;"">{right}</td>
    </tr>
  </table>";
            }

            // Se não houver imóveis, mostra placeholder
            if (imoveis.Count == 0)
            {
                unidadesHtml =
@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
    <tr>
      <td align=""center"" style=""padding:16px;border:2px dashed #DDDDDD;border-radius:6px;color:#999999;font-family:Arial,Helvetica,sans-serif;font-size:14px;"">
        Nenhum imóvel cadastrado.
      </td>
    </tr>
  </table>";
            }

            // ----- HTML do e-mail (tabelas + CSS inline) -----
            var html =
$@"<!DOCTYPE html>
<html lang=""pt-br"">
<head>
  <meta charset=""UTF-8"">
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>{System.Net.WebUtility.HtmlEncode(campanha.Titulo ?? "" )}</title>
  <style>body {{ margin:0; padding:0; mso-line-height-rule:exactly; -ms-text-size-adjust:100%; -webkit-text-size-adjust:100%; }}</style>
</head>
<body style=""Margin:0; padding:0; background-color:#F8F9FA;"">
  <center style=""width:100%; background:#F8F9FA;"">
    <!-- Texto acima do logo / acima da box -->
    {headerHtml}

    <!-- Container externo fluido -->
    <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background:#F8F9FA;"">
      <tr>
        <td align=""center"">
          <!-- Container interno 600px -->
          <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""600"" style=""width:600px; max-width:100%; background:#FFFFFF; border-radius:12px; box-shadow:0 2px 8px rgba(0,0,0,0.04);"">
            <tr>
              <td style=""padding:24px 24px 0 24px;text-align:center;"">
                <a href=""https://www.colnaghi.com.br"" target=""_blank"" rel=""noopener"" style=""display:inline-block;"">
                  <img src=""https://www.colnaghi.com.br/assets/images/logotipo.png"" alt=""Colnaghi Imóveis"" height=""25"" style=""height:25px; width:auto; border:0; outline:none; text-decoration:none;"">
                </a>
              </td>
            </tr>

            <tr>
              <td style=""padding:16px 0 0 0;"">
                <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                  <tr>
                    <td style=""border-top:1px solid #EEEEEE;"">&nbsp;</td>
                  </tr>
                </table>
              </td>
            </tr>

            <!-- Título -->
            <tr>
              <td align=""center"" style=""padding:16px 24px 8px 24px; font-family:Arial,Helvetica,sans-serif; color:#222222;"">
                <h1 style=""Margin:0; font-size:23px; line-height:1.3; font-weight:700; letter-spacing:0.02em;"">{System.Net.WebUtility.HtmlEncode(campanha.Titulo ?? "")}</h1>
              </td>
            </tr>

            <!-- Chamada -->
            <tr>
              <td align=""center"" style=""padding:0 24px 8px 24px; font-family:Arial,Helvetica,sans-serif; color:#666666;"">
                <p style=""Margin:0; font-size:15px; line-height:1.6;"">{System.Net.WebUtility.HtmlEncode(campanha.Chamada ?? "")}</p>
              </td>
            </tr>

            <!-- CTA (topo) -->
            {(string.IsNullOrEmpty(ctaHtmlTopo) ? "" : $@"<tr><td style=""padding:8px 24px 8px 24px;"">{ctaHtmlTopo}</td></tr>")}

            <!-- Corpo de texto -->
            <tr>
              <td align=""center"" style=""padding:8px 24px 24px 24px; font-family:Arial,Helvetica,sans-serif; color:#333333;"">
                <p style=""Margin:0; font-size:15px; line-height:1.6;"">{System.Net.WebUtility.HtmlEncode(campanha.CorpoTexto ?? "")}</p>
              </td>
            </tr>

            <!-- Título Unidades -->
            <tr>
              <td align=""center"" style=""padding:8px 24px 0 24px; font-family:Arial,Helvetica,sans-serif; color:#E53935; font-weight:700; font-size:18px;"">
                {System.Net.WebUtility.HtmlEncode(tituloUnidades)}
              </td>
            </tr>
            <tr>
              <td style=""padding:8px 24px 8px 24px;"">
                <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                  <tr><td style=""border-top:2px solid #E53935; width:60px; margin:0 auto; display:block; height:0;"">&nbsp;</td></tr>
                </table>
                <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                  <tr><td style=""border-top:2px solid #FFFFFF; width:60px; margin:0 auto; display:block; height:0;"">&nbsp;</td></tr>
                </table>
              </td>
            </tr>

            <!-- Grid de Unidades (2 colunas) -->
            <tr>
              <td style=""padding:0 24px 0 24px;"">
                {unidadesHtml}
              </td>
            </tr>

            <!-- CTA repetido abaixo das unidades -->
            {(string.IsNullOrEmpty(ctaHtmlRodape) ? "" : $@"<tr><td style=""padding:0 24px 16px 24px;"">{ctaHtmlRodape}</td></tr>")}

            <!-- Rodapé -->
            <tr>
              <td align=""center"" style=""padding:16px 24px 12px 24px; font-family:Arial,Helvetica,sans-serif; color:#888888;"">
                <p style=""Margin:0; font-size:14px; line-height:1.6;"">{System.Net.WebUtility.HtmlEncode(campanha.Rodape ?? "")}</p>
              </td>
            </tr>

            <tr>
              <td align=""center"" style=""padding:0 24px 24px 24px;"">
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                  <tr>
                    <td style=""padding:0 6px;"">
                      <a href=""https://www.instagram.com.br"" target=""_blank"" rel=""noopener"">
                        <img src=""https://cdn1.iconfinder.com/data/icons/social-media-circle-7/512/Circled_Instagram_svg-64.png"" alt=""Instagram"" width=""36"" height=""36"" style=""display:block;border:none;outline:none;"" />
                      </a>
                    </td>
                    <td style=""padding:0 6px;"">
                      <a href=""https://www.youtube.com/@colnaghi"" target=""_blank"" rel=""noopener"">
                        <img src=""https://cdn1.iconfinder.com/data/icons/social-media-circle-7/512/Circled_Youtube_svg-64.png"" alt=""YouTube"" width=""36"" height=""36"" style=""display:block;border:none;outline:none;"" />
                      </a>
                    </td>
                    <td style=""padding:0 6px;"">
                      <a href=""https://www.facebook.com/imoveiscolnaghi"" target=""_blank"" rel=""noopener"">
                        <img src=""https://cdn3.iconfinder.com/data/icons/social-media-black-white-2/512/BW_Facebook_glyph_svg-64.png"" alt=""Facebook"" width=""36"" height=""36"" style=""display:block;border:none;outline:none;"" />
                      </a>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
          <!-- /600 -->
        </td>
      </tr>
    </table>
  </center>
</body>
</html>";

            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            var fileName = $"campanha_{id}_email.html";
            return File(bytes, "text/html", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarImovel(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return BadRequest(new { success = false, message = "Informe o código do imóvel." });
            }

            try
            {
                var detalhes = await BuscarImovelNaApiAsync(codigo);
                if (detalhes == null)
                {
                    return NotFound(new { success = false, message = "Não encontramos um imóvel ativo com esse código." });
                }

                var tituloSugestao = ConstruirTituloImovel(detalhes.Tipo, detalhes.Bairro);
                var areaInteiraAjax = detalhes.AreaPrivativa.HasValue
                    ? (int)Math.Round(detalhes.AreaPrivativa.Value, MidpointRounding.AwayFromZero)
                    : (int?)null;
                var areaFormatada = areaInteiraAjax.HasValue ? $"{areaInteiraAjax.Value} m²" : string.Empty;
                var valorFormatado = detalhes.ValorVenda.HasValue
                    ? ((int)detalhes.ValorVenda.Value).ToString("C0", PtBrCulture)
                    : string.Empty;

                return Json(new
                {
                    success = true,
                    imovel = new
                    {
                        codigo = detalhes.CodigoImovel,
                        foto = detalhes.FotoDestaquePequena,
                        promocao = detalhes.Promocao,
                        tipo = detalhes.Tipo,
                        bairro = detalhes.Bairro,
                        tituloSugestao,
                        area = detalhes.AreaPrivativa,
                        areaFormatada,
                        vagas = detalhes.Vagas,
                        dormitorios = detalhes.Dormitorios,
                        valorVenda = detalhes.ValorVenda,
                        valorFormatado,
                        url = detalhes.UrlLink
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar imóvel {Codigo}", codigo);
                return StatusCode(500, new { success = false, message = "Não foi possível buscar esse imóvel agora. Tente novamente em instantes." });
            }
        }

        private async Task<CampanhaImovel?> BuscarImovelNaApiAsync(string codigo)
        {
            var sanitized = codigo?.Trim();
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return null;
            }

            var url = string.Format(CultureInfo.InvariantCulture, VistaDetalhesUrl, Uri.EscapeDataString(sanitized));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Content-Type", "application/json");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var imovel = new CampanhaImovel
            {
                CodigoImovel = sanitized,
                ValorVenda = ParseDecimal(root.GetPropertyOrNull("ValorVenda")?.GetString()),
                Dormitorios = ParseInt(root.GetPropertyOrNull("Dormitorios")?.GetString()),
                Vagas = ParseInt(root.GetPropertyOrNull("Vagas")?.GetString()),
                AreaPrivativa = ParseDecimal(root.GetPropertyOrNull("AreaPrivativa")?.GetString()),
                ValorVendaEspecial = ParseDecimal(root.GetPropertyOrNull("PrecoEspecial")?.GetString()),
                Promocao = root.GetPropertyOrNull("Promocao")?.GetString(),
                Tipo = root.GetPropertyOrNull("Categoria")?.GetString(),
                Bairro = root.GetPropertyOrNull("Bairro")?.GetString(),
                FotoDestaquePequena = root.GetPropertyOrNull("FotoDestaquePequena")?.GetString()
            };

            imovel.UrlLink = MontarUrlImovel(imovel);

            return imovel;
        }

        private static string MontarUrlImovel(CampanhaImovel imovel)
        {
            if (string.IsNullOrWhiteSpace(imovel.CodigoImovel))
            {
                return string.Empty;
            }

            var tipoSlug = Slug(imovel.Tipo);
            var bairroSlug = Slug(imovel.Bairro);
            var dorms = imovel.Dormitorios.HasValue
                ? imovel.Dormitorios.Value.ToString(CultureInfo.InvariantCulture)
                : "0";

            return $"https://www.colnaghi.com.br/imovel/{tipoSlug}-{bairroSlug}-{dorms}-dorms-porto-alegre-venda-{imovel.CodigoImovel}";
        }

        private static string ConstruirTituloImovel(string? tipo, string? bairro)
        {
            var tipoFormatado = Capitalizar(tipo);
            var bairroFormatado = Capitalizar(bairro);

            if (string.IsNullOrEmpty(tipoFormatado) && string.IsNullOrEmpty(bairroFormatado))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(tipoFormatado))
            {
                return bairroFormatado;
            }

            if (string.IsNullOrEmpty(bairroFormatado))
            {
                return tipoFormatado;
            }

            return $"{tipoFormatado} no {bairroFormatado}";
        }

        private static string Capitalizar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }

            var lower = texto.Trim().ToLower(PtBrCulture);
            return PtBrTextInfo.ToTitleCase(lower);
        }

        private static decimal? ParseDecimal(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var texto = raw.Trim();
            texto = texto.Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

            if (texto.Contains(','))
            {
                texto = texto.Replace(".", string.Empty);
                texto = texto.Replace(',', '.');
            }

            if (decimal.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var valor))
            {
                return valor;
            }

            if (decimal.TryParse(raw, NumberStyles.Any, PtBrCulture, out valor))
            {
                return valor;
            }

            return null;
        }

        private static int? ParseInt(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var texto = raw.Trim();
            if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor))
            {
                return valor;
            }

            if (int.TryParse(texto, NumberStyles.Integer, PtBrCulture, out valor))
            {
                return valor;
            }

            return null;
        }

        private static string Slug(string? texto)
        {
            var semAcento = RemoverAcentos(texto).ToLowerInvariant();
            var sb = new StringBuilder(semAcento.Length);

            foreach (var caractere in semAcento)
            {
                if (char.IsLetterOrDigit(caractere))
                {
                    sb.Append(caractere);
                }
                else if (char.IsWhiteSpace(caractere) || caractere == '-' || caractere == '_')
                {
                    sb.Append('-');
                }
            }

            var slug = sb.ToString().Trim('-');
            while (slug.Contains("--", StringComparison.Ordinal))
            {
                slug = slug.Replace("--", "-", StringComparison.Ordinal);
            }

            return slug;
        }

        private static string RemoverAcentos(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }

            var normalizado = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var caractere in normalizado)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(caractere);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static Dictionary<string, string> ParseCustomTitles(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return parsed != null
                    ? new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
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
