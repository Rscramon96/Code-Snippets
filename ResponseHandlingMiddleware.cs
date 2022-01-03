using HomeBanking.GatewayInterno.Model.ViewModels.Enums;
using HomeBanking.GatewayInterno.Service.Helpers;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HomeBanking.GatewayInterno.Config
{
    public class ResponseHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        public ResponseHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var originalBodyStream = context.Response.Body;

            using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            await _next.Invoke(context);

            if (context.Response.StatusCode != 400)
            {
                await ProccessResponse(context);

                context.Response.Body.Seek(0, SeekOrigin.Begin);
                await context.Response.Body.CopyToAsync(originalBodyStream);
            }
            else if (context.Response.StatusCode == 400)
            {
                memoryStream.Seek(0, SeekOrigin.Begin);
                using var bufferReader = new StreamReader(memoryStream);
                string responseBody = await bufferReader.ReadToEndAsync();

                memoryStream.Position = 0;
                memoryStream.SetLength(0);

                await GetCustomerErrorRequestResponse(context, responseBody);

                context.Response.Body.Seek(0, SeekOrigin.Begin);
                await context.Response.Body.CopyToAsync(originalBodyStream);
            }
        }

        /// <summary>
        /// Processa a resposta da requisição. EXCETO resposta com código 400
        /// </summary>
        /// <param name="context"></param>
        /// <param name="responseBody"></param>
        /// <returns></returns>
        private static async Task ProccessResponse(HttpContext context)
        {
            switch (context.Response.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                case 204:
                    await GetResponseFromSuccessRequest(context);
                    break;
                case 401:
                    break;
                case 403:
                    break;
                case 404:
                    break;
                case 422:
                    break;
                case 429:
                    break;
                case 500:
                case 501:
                case 502:
                case 503:
                    await GetErrorRequestResponseFromServer(context);
                    break;
                default:
                    await GetErrorRequestResponseDefault(context);
                    break;
            }
        }

        /// <summary>
        /// Retorna a resposta para erros 5XX
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static async Task GetErrorRequestResponseFromServer(HttpContext context)
        {
            await context.Response.WriteAsJsonAsync(GenericHelpers.GenerateResponseDefault(new string[] { "Ocorreu uma falha inesperada. Tente novamente mais tarde." }));
        }

        /// <summary>
        /// Retorna a respota padrão caso o código não tenha tratamento
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static async Task GetErrorRequestResponseDefault(HttpContext context)
        {
            await context.Response.WriteAsJsonAsync(GenericHelpers.GenerateResponseDefault(new string[] { "Ocorreu uma falha inesperada, caso persista, entre em contato com o atendimento." }));
        }

        /// <summary>
        /// Retorna a resposta para erros 400
        /// </summary>
        /// <param name="context"></param>
        /// <param name="responseBody"></param>
        /// <returns></returns>
        private static async Task GetCustomerErrorRequestResponse(HttpContext context, string responseBody)
        {
            switch (context.Request.Method)
            {
                case "DELETE":
                case "GET":
                case "POST":
                case "PUT":
                    string[] messages = Array.Empty<string>();

                    try 
                    { 
                        messages = JsonConvert.DeserializeObject<string[]>(responseBody); 
                    }
                    catch 
                    {
                        messages = new string[] { responseBody };
                    }

                    await context.Response.WriteAsJsonAsync(GenericHelpers.GenerateResponseDefault(messages, EErrorType.Warning));
                    break;
                default:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(GenericHelpers.GenerateResponseDefault(new string[] { "Ocorreu uma falha inesperada ao tentar realizar esta operação." }, EErrorType.Warning));
                    break;
            }
        }

        /// <summary>
        /// Retorna a resposta para erros 2XX
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static async Task GetResponseFromSuccessRequest(HttpContext context)
        {
            switch (context.Request.Method)
            {
                case "GET":
                    break;
                case "POST":
                    break;
                case "PUT":
                    break;
                case "DELETE":
                    await context.Response.WriteAsJsonAsync(GenericHelpers.GenerateResponseDefault(new string[] { "Excluído com sucesso." }, EErrorType.Success));
                    break;
                default:
                    break;
            }
        }
    }
}
