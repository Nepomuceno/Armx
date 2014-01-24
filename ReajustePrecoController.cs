using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Omint.GestaoDeProdutos.Dominio;
using Omint.GestaoDeProdutos.Models;
using Omint.GestaoDeProdutos.Regra;
using Omint.GestaoDeProdutos.Seguranca;

namespace Omint.GestaoDeProdutos.Controllers
{
    [HandleError(ExceptionType = typeof(OmintException), View = "OmintException", Order = 1)]
    [HandleError(ExceptionType = typeof(Exception), View = "OmintException", Order = 2)]
    public class ReajustePrecoController : Controller
    {
        private readonly IReajustePreco _reajustes;

        public ReajustePrecoController(IReajustePreco reajustePreco)
        {
            _reajustes = reajustePreco;
        }

        private Dictionary<List<ReajusteDTO>, bool> TransformaVigenciaPrecoParaDTO(Dictionary<List<VigenciaPreco>, bool> registrosDeVigencias)
        {
            Dictionary<List<ReajusteDTO>, bool> registrosDeVigenciasDTO = new Dictionary<List<ReajusteDTO>, bool>();
            
            foreach (var listaDeVigenciaPreco in registrosDeVigencias.Keys)
            {
                List<ReajusteDTO> reajusteDtos = new List<ReajusteDTO>();

                foreach (var vigenciaPreco in listaDeVigenciaPreco)
                {
                    var dto = new ReajusteDTO()
                    {
                        Plano = vigenciaPreco.CodigoDoPlano,
                        FaixaEtaria = vigenciaPreco.FaixaEtaria,
                        DataInicio = vigenciaPreco.DataInicioVigencia,
                        DataFim = vigenciaPreco.DataFimVigencia,
                        Preco = vigenciaPreco.Preco,
                        TipoRegistro = (TipoRegistro)vigenciaPreco.OrigemRegistro
                    };
                                          
                    reajusteDtos.Add(dto);                    
                }
                
                //Captura o valor da chave no dicionário
                bool registrosComInconsistencias;
                registrosDeVigencias.TryGetValue(listaDeVigenciaPreco, out registrosComInconsistencias);

                //Adiciona lista de ReajusteDTO no dicionário
                registrosDeVigenciasDTO.Add(reajusteDtos, registrosComInconsistencias);                    
            }

            return registrosDeVigenciasDTO;
        }

        private Dictionary<List<VigenciaPreco>, bool> TransformaDTOParaVigenciaPreco(Dictionary<List<ReajusteDTO>, bool> registrosDeVigenciasDTO)
        {
            Dictionary<List<VigenciaPreco>, bool> registrosDeVigencias = new Dictionary<List<VigenciaPreco>, bool>();

            foreach (var listaDeVigenciaPrecoDTO in registrosDeVigenciasDTO.Keys)
            {
                List<VigenciaPreco> vigenciasPreco = new List<VigenciaPreco>();

                foreach (var vigenciaPrecoDTO in listaDeVigenciaPrecoDTO)
                {
                    var vigencia = new VigenciaPreco(vigenciaPrecoDTO.Plano, vigenciaPrecoDTO.FaixaEtaria,
                                                vigenciaPrecoDTO.Preco,
                                                (OrigemRegistroVigencia) vigenciaPrecoDTO.TipoRegistro,
                                                vigenciaPrecoDTO.DataInicio, vigenciaPrecoDTO.DataFim)
                        {
                            ManterRegistro = vigenciaPrecoDTO.Manter
                        };

                    vigenciasPreco.Add(vigencia);
                }

                //Captura o valor da chave no dicionário
                bool registrosComInconsistencias;
                registrosDeVigenciasDTO.TryGetValue(listaDeVigenciaPrecoDTO, out registrosComInconsistencias);

                //Adiciona lista de ReajusteDTO no dicionário
                registrosDeVigencias.Add(vigenciasPreco, registrosComInconsistencias);
            }

            return registrosDeVigencias;
        }


        [HttpPost]
        [AutorizacaoOmint(Roles = "manutencao")]
        [HandleError(ExceptionType = typeof(OmintException), View = "OmintException", Order = 1)]
        [HandleError(ExceptionType = typeof(Exception), View = "OmintException", Order = 2)]
        public ActionResult RecuperaRegistros(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength <= 0)
                return RedirectToAction("ReajustarPreco", "Home");

            if (!string.IsNullOrEmpty(Path.GetFileName(file.FileName)))
            {

                try
                {
                    Dictionary<List<VigenciaPreco>, bool> registrosDeVigencias = new Dictionary<List<VigenciaPreco>, bool>();
                    Dictionary<List<ReajusteDTO>, bool> registrosDeVigenciasDTO = new Dictionary<List<ReajusteDTO>, bool>();

                    if (_reajustes.ValidaModeloArquivo(file.InputStream))
                    {
                        if (_reajustes.ProcessarReajuste(file.InputStream, ref registrosDeVigencias))
                        {
                            registrosDeVigenciasDTO = TransformaVigenciaPrecoParaDTO(registrosDeVigencias);
                            return View("ConfirmarOperacao", registrosDeVigenciasDTO);
                        }

                        ViewBag.MensagemInconsistencia = "Os registros possuem inconsistências";
                        registrosDeVigenciasDTO = TransformaVigenciaPrecoParaDTO(registrosDeVigencias);
                        return View(registrosDeVigenciasDTO);
                        
                    }
                }
                catch (OmintException ex)
                {
                    ex.MensagemParaUsuario = "Há inconsistências na requisição";
                    throw new OmintException(String.Format("Inconsistências na validação das regras"), ex, "Há inconsistências na requisição");
                }
                catch (Exception ex)
                {
                    string mensagenTecnica = String.Format("Incosistências na validação das regras");
                    throw new OmintException(mensagenTecnica, ex, "Há inconsistências na requisição");
                }
            }

            return View("ErroProcessamento");
        }


        [HttpPost]
        [AutorizacaoOmint(Roles = "manutencao")]
        [HandleError(ExceptionType = typeof(OmintException), View = "OmintException", Order = 1)]
        public ActionResult CorrigeInconsistencias(IEnumerable<ReajusteDTO> registrosDeVigenciasDaView)
        {
            Dictionary<List<ReajusteDTO>, bool> registrosDeVigenciasDTO = new Dictionary<List<ReajusteDTO>, bool>();
            List<ReajusteDTO> reajusteDtos = new List<ReajusteDTO>();

            foreach (var registroVigenciaDTO in registrosDeVigenciasDaView)
            {
                reajusteDtos.Add(registroVigenciaDTO);
            }
            registrosDeVigenciasDTO.Add(reajusteDtos, true);

            try
            {

                var registrosDeVigencias = TransformaDTOParaVigenciaPreco(registrosDeVigenciasDTO);

                if (_reajustes.CorrigeInconsistencias(ref registrosDeVigencias))
                {
                    registrosDeVigenciasDTO = TransformaVigenciaPrecoParaDTO(registrosDeVigencias);
                    return View("ConfirmarOperacao", registrosDeVigenciasDTO);
                }


                ViewBag.MensagemInconsistencia = "Os registros ainda possuem inconsistências";

                registrosDeVigenciasDTO = TransformaVigenciaPrecoParaDTO(registrosDeVigencias);
                return View("RecuperaRegistros", registrosDeVigenciasDTO);
            }
            catch (OmintException ex)
            {
                ex.MensagemParaUsuario = "Os registros possuem inconsistências";
                throw;
            }
            catch (Exception ex)
            {
                string mensagenTecnica = String.Format("Incosistências na validação das regras");
                throw new OmintException(mensagenTecnica, ex, "Há inconsistências na requisição");
            }            
        }


        public ActionResult ConfirmarOperacao(IEnumerable<ReajusteDTO> registrosDeVigenciasDaView)
        {
            try
            {
                List<VigenciaPreco> registrosDeVigencias = new List<VigenciaPreco>();
                List<VigenciaPreco> registros = new List<VigenciaPreco>();

                foreach (var registroVigenciaDaView in registrosDeVigenciasDaView)
                {
                    var vigenciaPreco = new VigenciaPreco(registroVigenciaDaView.Plano, registroVigenciaDaView.FaixaEtaria,
                                                          registroVigenciaDaView.Preco, (OrigemRegistroVigencia)registroVigenciaDaView.TipoRegistro,
                                                          registroVigenciaDaView.DataInicio, registroVigenciaDaView.DataFim);

                    registrosDeVigencias.Add(vigenciaPreco);
                }

                
                _reajustes.AplicaAlteracoes(registrosDeVigencias, HttpContext.User.Identity.Name.Split("\\".ToCharArray())[1]);

                return View("OperacaoRealizada");
            }
            catch (OmintException ex)
            {
                ex.MensagemParaUsuario = "Ocorreu um erro durante o processamento. Contate o suporte para maiores informações.";
                throw;
            }
            catch (Exception ex)
            {
                string mensagenTecnica = String.Format("Problemas na camada de persistência ao tentar aplicar alterações.");
                throw new OmintException(mensagenTecnica, ex, "Ocorreu um erro durante o processamento. Contate o suporte para maiores informações.");
            }
        }

    }
}
