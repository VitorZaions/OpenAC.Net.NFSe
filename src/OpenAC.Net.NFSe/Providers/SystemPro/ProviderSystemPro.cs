// ***********************************************************************
// Assembly         : OpenAC.Net.NFSe
// Author           : Felipe Silveira (Transis Software)
// Created          : 18-08-2021
//
// Last Modified By : Felipe Silveira (Transis Software)
// Last Modified On : 30-03-2022
// ***********************************************************************
// <copyright file="ProviderSystemPro.cs" company="OpenAC .Net">
//		        		   The MIT License (MIT)
//	     		Copyright (c) 2014 - 2024 Projeto OpenAC .Net
//
//	 Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//	 The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//	 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// </copyright>
// <summary></summary>
// ***********************************************************************

using OpenAC.Net.Core.Extensions;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.DFe.Core.Serializer;
using OpenAC.Net.NFSe.Configuracao;
using OpenAC.Net.NFSe.Nota;
using System.Linq;
using System;
using System.Text;
using System.Xml.Linq;
using OpenAC.Net.NFSe.Commom;
using OpenAC.Net.NFSe.Commom.Interface;
using OpenAC.Net.NFSe.Commom.Model;
using OpenAC.Net.NFSe.Commom.Types;
using System.Web;
using System.Collections.Generic;
using OpenAC.Net.Core;
using System.Xml;
using OpenAC.Net.DFe.Core.Document;

namespace OpenAC.Net.NFSe.Providers;

internal sealed class ProviderSystemPro : ProviderABRASF201
{
    #region Constructors

    public ProviderSystemPro(ConfigNFSe config, OpenMunicipioNFSe municipio) : base(config, municipio)
    {
        Name = "SystemPro";
    }

    #endregion Constructors

    #region Methods

    #region Protected Methods

    protected override string GerarCabecalho()
    {
        var cabecalho = new System.Text.StringBuilder();
        cabecalho.Append("<cabecalho versao=\"2.01\" xmlns=\"http://www.abrasf.org.br/nfse.xsd\">");
        cabecalho.Append("<versaoDados>2.01</versaoDados>");
        cabecalho.Append("</cabecalho>");
        return cabecalho.ToString();
    }

    protected override void AssinarEnviar(RetornoEnviar retornoWebservice)
    {
        retornoWebservice.XmlEnvio = XmlSigning.AssinarXml(retornoWebservice.XmlEnvio, "EnviarLoteRpsEnvio", "LoteRps", Certificado);
    }

    protected override void AssinarEnviarSincrono(RetornoEnviar retornoWebservice)
    {
        retornoWebservice.XmlEnvio = XmlSigning.AssinarXml(retornoWebservice.XmlEnvio, "EnviarLoteRpsSincronoEnvio", "LoteRps", Certificado);
    }

    protected override IServiceClient GetClient(TipoUrl tipo) => new SystemProServiceClient(this, tipo, Certificado);

    #endregion Protected Methods

    #region RPS

    protected override XElement WriteServicosRps(NotaServico nota)
    {
        var servico = new XElement("Servico");

        servico.Add(WriteValoresRps(nota));

        servico.AddChild(AddTag(TipoCampo.Int, "", "IssRetido", 1, 1, Ocorrencia.Obrigatoria, nota.Servico.Valores.IssRetido == SituacaoTributaria.Retencao ? 1 : 2));

        if (nota.Servico.ResponsavelRetencao.HasValue)
            servico.AddChild(AddTag(TipoCampo.Int, "", "ResponsavelRetencao", 1, 1, Ocorrencia.NaoObrigatoria, (int)nota.Servico.ResponsavelRetencao + 1));

        servico.AddChild(AddTag(TipoCampo.Str, "", "ItemListaServico", 1, 5, Ocorrencia.Obrigatoria, nota.Servico.ItemListaServico));
        servico.AddChild(AddTag(TipoCampo.Str, "", "Discriminacao", 1, 2000, Ocorrencia.Obrigatoria, nota.Servico.Discriminacao));
        servico.AddChild(AddTag(TipoCampo.Str, "", "CodigoMunicipio", 1, 20, Ocorrencia.Obrigatoria, nota.Servico.CodigoMunicipio));
        servico.AddChild(AddTag(TipoCampo.Int, "", "ExigibilidadeISS", 1, 1, Ocorrencia.Obrigatoria, (int)nota.Servico.ExigibilidadeIss + 1));
        servico.AddChild(AddTag(TipoCampo.Int, "", "MunicipioIncidencia", 7, 7, Ocorrencia.MaiorQueZero, nota.Servico.MunicipioIncidencia));

        return servico;
    }

    #endregion RPS

    #region Services

    protected override void TratarRetornoEnviarSincrono(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
    {
        // Analisa mensagem de retorno
        var xmlRet = XDocument.Parse(retornoWebservice.XmlRetorno);
        MensagemErro(retornoWebservice, xmlRet.Root, "EnviarLoteRpsSincronoResposta");
        MensagemErro(retornoWebservice, xmlRet.Root, "ListaMensagemRetorno");
        MensagemErro(retornoWebservice, xmlRet.Root, "ListaMensagemRetornoLote");
        if (retornoWebservice.Erros.Any()) return;

        // Primeiro, extraia o conte�do do elemento <return>
        var returnElement = xmlRet.Root?.Value;

        // Decodifique o conte�do XML escapado
        var decodedXml = System.Net.WebUtility.HtmlDecode(returnElement);

        // Parseie o XML decodificado
        var innerXml = XDocument.Parse(decodedXml);
        
        retornoWebservice.Data = innerXml.Root?.ElementAnyNs("DataRecebimento")?.GetValue<DateTime>() ?? DateTime.MinValue;
        retornoWebservice.Protocolo = innerXml.Root?.ElementAnyNs("ListaNfse")?.ElementAnyNs("CompNfse")?.ElementAnyNs("Nfse")?.ElementAnyNs("InfNfse")?.ElementAnyNs("CodigoVerificacao")?.GetValue<string>() ?? string.Empty;
        retornoWebservice.NumeroNFSe = innerXml.Root?.ElementAnyNs("ListaNfse")?.ElementAnyNs("CompNfse")?.ElementAnyNs("Nfse")?.ElementAnyNs("InfNfse")?.ElementAnyNs("Numero")?.GetValue<string>() ?? string.Empty;

        retornoWebservice.Sucesso = !retornoWebservice.Protocolo.IsEmpty();

        if (!retornoWebservice.Sucesso) return;

        var listaNfse = innerXml.Root?.ElementAnyNs("ListaNfse");

        if (listaNfse == null)
        {
            retornoWebservice.Erros.Add(new EventoRetorno { Codigo = "0", Descricao = "Lista de NFSe n�o encontrada! (ListaNfse)" });
            retornoWebservice.Sucesso = false;
            return;
        }

        retornoWebservice.Sucesso = true;

        foreach (var compNfse in listaNfse.ElementsAnyNs("CompNfse"))
        {
            var nfse = compNfse.ElementAnyNs("Nfse").ElementAnyNs("InfNfse");
            var numeroNFSe = nfse.ElementAnyNs("Numero")?.GetValue<string>() ?? string.Empty;
            var chaveNFSe = nfse.ElementAnyNs("CodigoVerificacao")?.GetValue<string>() ?? string.Empty;
            var dataNFSe = nfse.ElementAnyNs("DataEmissao")?.GetValue<DateTime>() ?? DateTime.Now;
            var numeroRps = nfse?.ElementAnyNs("DeclaracaoPrestacaoServico")?.ElementAnyNs("InfDeclaracaoPrestacaoServico")?.ElementAnyNs("Rps")?.ElementAnyNs("IdentificacaoRps")?.ElementAnyNs("Numero")?.GetValue<string>() ?? string.Empty;
            GravarNFSeEmDisco(compNfse.AsString(true), $"NFSe-{numeroNFSe}-{chaveNFSe}-.xml", dataNFSe);

            var nota = notas.FirstOrDefault(x => x.IdentificacaoRps.Numero == numeroRps);
            if (nota == null)
            {
                notas.Load(compNfse.ToString());
            }
            else
            {
                nota.IdentificacaoNFSe.Numero = numeroNFSe;
                nota.IdentificacaoNFSe.Chave = chaveNFSe;
                nota.IdentificacaoNFSe.DataEmissao = dataNFSe;
                nota.XmlOriginal = compNfse.AsString();
            }
        }
    }

    protected override void MensagemErro(RetornoWebservice retornoWs, XContainer xmlRet, string xmlTag)
    {
        if (xmlRet != null)
        {
            string XMLFinal = ((System.Xml.Linq.XElement)xmlRet).Value;

            if (!string.IsNullOrEmpty(XMLFinal))
            {
                var decodedXmlString = HttpUtility.HtmlDecode(XMLFinal);
                XDocument decodedXml = XDocument.Parse(decodedXmlString);

                var mensagens = decodedXml?.ElementAnyNs(xmlTag);
                mensagens = mensagens?.ElementAnyNs("ListaMensagemRetorno");
                if (mensagens != null)
                {
                    foreach (var mensagem in mensagens.ElementsAnyNs("MensagemRetorno"))
                    {
                        var evento = new EventoRetorno
                        {
                            Codigo = mensagem?.ElementAnyNs("Codigo")?.GetValue<string>() ?? string.Empty,
                            Descricao = mensagem?.ElementAnyNs("Mensagem")?.GetValue<string>() ?? string.Empty,
                            Correcao = mensagem?.ElementAnyNs("Correcao")?.GetValue<string>() ?? string.Empty
                        };

                        retornoWs.Erros.Add(evento);
                    }
                }

                mensagens = xmlRet?.ElementAnyNs(xmlTag);
                mensagens = mensagens?.ElementAnyNs("ListaMensagemRetornoLote");
                if (mensagens == null) return;
                {
                    foreach (var mensagem in mensagens.ElementsAnyNs("MensagemRetorno"))
                    {
                        var evento = new EventoRetorno
                        {
                            Codigo = mensagem?.ElementAnyNs("Codigo")?.GetValue<string>() ?? string.Empty,
                            Descricao = mensagem?.ElementAnyNs("Mensagem")?.GetValue<string>() ?? string.Empty,
                            IdentificacaoRps = new IdeRps()
                            {
                                Numero = mensagem?.ElementAnyNs("IdentificacaoRps")?.ElementAnyNs("Numero")?.GetValue<string>() ?? string.Empty,
                                Serie = mensagem?.ElementAnyNs("IdentificacaoRps")?.ElementAnyNs("Serie")?.GetValue<string>() ?? string.Empty,
                                Tipo = mensagem?.ElementAnyNs("IdentificacaoRps")?.ElementAnyNs("Tipo")?.GetValue<TipoRps>() ?? TipoRps.RPS,
                            }
                        };

                        retornoWs.Erros.Add(evento);
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    protected override void TratarRetornoConsultarNFSe(RetornoConsultarNFSe retornoWebservice, NotaServicoCollection notas)
    {
        // Analisa mensagem de retorno
        var xmlRet = XDocument.Parse(retornoWebservice.XmlRetorno);
        MensagemErro(retornoWebservice, xmlRet.Root, "ConsultarNfseFaixaResposta");

        // Primeiro, extraia o conte�do do elemento <return>
        var returnElement = xmlRet.Root?.Value;

        // Decodifique o conte�do XML escapado
        var decodedXml = System.Net.WebUtility.HtmlDecode(returnElement);

        // Parseie o XML decodificado
        var innerXml = XDocument.Parse(decodedXml);

        if (retornoWebservice.Erros.Any()) return;

        var retornoLote = innerXml.ElementAnyNs("ConsultarNfseFaixaResposta");
        var listaNfse = retornoLote?.ElementAnyNs("ListaNfse");
        if (listaNfse == null)
        {
            retornoWebservice.Erros.Add(new EventoRetorno { Codigo = "0", Descricao = "Lista de NFSe não encontrada! (ListaNfse)" });
            retornoWebservice.Sucesso = false;
            return;
        }

        var notasServico = new List<NotaServico>();
        retornoWebservice.Sucesso = true;

        foreach (var compNfse in listaNfse.ElementsAnyNs("CompNfse"))
        {
            // Carrega a nota fiscal na coleção de Notas Fiscais
            var nota = LoadXml(compNfse.AsString());

            GravarNFSeEmDisco(compNfse.AsString(true), $"NFSe-{nota.IdentificacaoNFSe.Numero}-{nota.IdentificacaoNFSe.Chave}-.xml", nota.IdentificacaoNFSe.DataEmissao);

            notasServico.Add(nota);
            notas.Add(nota);
        }

        retornoWebservice.ProximaPagina = listaNfse.ElementAnyNs("ProximaPagina")?.GetValue<int>() ?? 0;
        retornoWebservice.Notas = notasServico.ToArray();
    }

    /// <inheritdoc />

    public override NotaServico LoadXml(XDocument xml)
    {
        Guard.Against<XmlException>(xml == null, "Xml invalido.");

        // Primeiro, extraia o conte�do do elemento <return>
        //var returnElement = xml.Root?.Value;

        // Decodifique o conte�do XML escapado
        //var decodedXml = System.Net.WebUtility.HtmlDecode(xml);

        // Parseie o XML decodificado
        //var innerXml = XDocument.Parse(decodedXml);

        XElement rootNFSe = null;
        XElement rootCanc = null;
        XElement rootSub = null;
        XElement rootRps;


        XElement? rootLista = null;
        XElement? rootGrupo = null;

        var rootResposta = xml.ElementAnyNs("EnviarLoteRpsSincronoResposta");

        if (rootResposta != null) // Imprimir
        {
            rootLista = rootResposta.ElementAnyNs("ListaNfse");
            rootGrupo = rootLista.ElementAnyNs("CompNfse");
        }
        else // Consulta
        {
            rootGrupo = xml.ElementAnyNs("CompNfse");
        }

        if (rootGrupo != null)
        {
            rootNFSe = rootGrupo.ElementAnyNs("Nfse")?.ElementAnyNs("InfNfse");
            rootCanc = rootGrupo.ElementAnyNs("NfseCancelamento");
            rootSub = rootGrupo.ElementAnyNs("NfseSubstituicao");
            rootRps = rootNFSe.ElementAnyNs("DeclaracaoPrestacaoServico")?.ElementAnyNs("InfDeclaracaoPrestacaoServico");
        }
        else
        {
            rootRps = xml.ElementAnyNs("Rps").ElementAnyNs("InfDeclaracaoPrestacaoServico");
        }

        Guard.Against<XmlException>(rootNFSe == null && rootRps == null, "Xml de RPS ou NFSe invalido.");

        var ret = new NotaServico(Configuracoes)
        {
            XmlOriginal = xml.AsString()
        };

        if (rootRps != null) //Goiania não retorna o RPS, somente a NFSe
            LoadRps(ret, rootRps);

        if (rootNFSe == null) return ret;

        LoadNFSe(ret, rootNFSe);
        if (rootSub != null) LoadNFSeSub(ret, rootSub);
        if (rootCanc != null) LoadNFSeCancelada(ret, rootCanc);

        return ret;
    }

    protected override void TratarRetornoCancelarNFSe(RetornoCancelar retornoWebservice, NotaServicoCollection notas)
    {
        // Analisa mensagem de retorno
        var xmlRet = XDocument.Parse(retornoWebservice.XmlRetorno);

        // Primeiro, extraia o conte�do do elemento <return>
        var returnElement = xmlRet.Root?.Value;

        // Decodifique o conte�do XML escapado
        var decodedXml = System.Net.WebUtility.HtmlDecode(returnElement);

        // Parseie o XML decodificado
        var innerXml = XDocument.Parse(decodedXml);

        MensagemErro(retornoWebservice, xmlRet.Root, "CancelarNfseResposta");
        if (retornoWebservice.Erros.Any()) return;

        var confirmacaoCancelamento = innerXml.ElementAnyNs("CancelarNfseResposta")?.ElementAnyNs("RetCancelamento")?.ElementAnyNs("NfseCancelamento")?.ElementAnyNs("Confirmacao")?.ElementAnyNs("Pedido")?.ElementAnyNs("InfPedidoCancelamento");
        var confirmacaoDataHora = innerXml.ElementAnyNs("CancelarNfseResposta")?.ElementAnyNs("RetCancelamento")?.ElementAnyNs("NfseCancelamento")?.ElementAnyNs("Confirmacao");
        if (confirmacaoCancelamento == null)
        {
            retornoWebservice.Erros.Add(new EventoRetorno { Codigo = "0", Descricao = "Confirmação do cancelamento não encontrada!" });
            return;
        }

        // Se a nota fiscal cancelada existir na coleção de Notas Fiscais, atualiza seu status:
        //var nota = notas.FirstOrDefault(x => x.IdentificacaoNFSe.Numero.Trim() == retornoWebservice.NumeroNFSe);
        //if (retornoWebservice.XmlRetorno == null) return;

        retornoWebservice.Data = confirmacaoDataHora.ElementAnyNs("DataHora")?.GetValue<DateTime>() ?? DateTime.MinValue;
        retornoWebservice.Sucesso = retornoWebservice.Data != DateTime.MinValue;

        //nota.Situacao = SituacaoNFSeRps.Cancelado;
        //nota.Cancelamento.Pedido.CodigoCancelamento = retornoWebservice.CodigoCancelamento;
        //nota.Cancelamento.DataHora = retornoWebservice.Data;
        //nota.Cancelamento.MotivoCancelamento = retornoWebservice.Motivo;
        //nota.Cancelamento.Signature = confirmacaoDataHora.ElementAnyNs("Pedido").ElementAnyNs("Signature") != null ? DFeSignature.Load(confirmacaoDataHora.ElementAnyNs("Pedido").ElementAnyNs("Signature")?.ToString()) : null;
    }


    protected override void PrepararConsultarNFSe(RetornoConsultarNFSe retornoWebservice)
    {
        if (retornoWebservice.NumeroNFse == 0)
        {
            throw new System.Exception("Faltou informar o numero da NFSe para consulta por faixa");
        }
        var loteBuilder = new StringBuilder();
        loteBuilder.Append($"<ConsultarNfseFaixaEnvio {GetNamespace()}>");
        loteBuilder.Append("<Prestador>");
        loteBuilder.Append("<CpfCnpj>");
        loteBuilder.Append(Configuracoes.PrestadorPadrao.CpfCnpj.IsCNPJ()
            ? $"<Cnpj>{Configuracoes.PrestadorPadrao.CpfCnpj.ZeroFill(14)}</Cnpj>"
            : $"<Cpf>{Configuracoes.PrestadorPadrao.CpfCnpj.ZeroFill(11)}</Cpf>");
        loteBuilder.Append("</CpfCnpj>");
        if (!Configuracoes.PrestadorPadrao.InscricaoMunicipal.IsEmpty()) loteBuilder.Append($"<InscricaoMunicipal>{Configuracoes.PrestadorPadrao.InscricaoMunicipal}</InscricaoMunicipal>");
        loteBuilder.Append("</Prestador>");

        loteBuilder.Append("<Faixa>");
        loteBuilder.Append($"<NumeroNfseInicial>{retornoWebservice.NumeroNFse}</NumeroNfseInicial>");
        loteBuilder.Append($"<NumeroNfseFinal>{retornoWebservice.NumeroNFse}</NumeroNfseFinal>");
        loteBuilder.Append("</Faixa>");
        loteBuilder.Append($"<Pagina>{System.Math.Max(retornoWebservice.Pagina, 1)}</Pagina>");
        loteBuilder.Append("</ConsultarNfseFaixaEnvio>");

        retornoWebservice.XmlEnvio = loteBuilder.ToString();
    }

    #endregion Services

    #endregion Methods
}