using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

public class RetoSesion3
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== SESION 3: RETO SEMILLERO - Comparar tres versiones de un prompt ===\n");
        var client = new AnthropicClient(); // lee ANTHROPIC_API_KEY del environment

        var texto = "Hola, mi nombre es Juan Perez. Hice un pedido la semana pasada, el numero de orden es ORD-9982. El total que pague fue de $150.50 y me dijeron que llegaria en 3 dias pero aun no llega.";

        var promptVago = $"Extrae detalles de este texto: {texto}";

        var promptEspecifico = $@"Extrae el nombre del cliente, numero de orden y monto total del siguiente texto. Devuelve el resultado en formato JSON.
Texto: {texto}";

        var promptXmlTags = $@"Extrae el nombre del cliente, numero de orden y monto total del texto proporcionado.
Devuelve el resultado en formato JSON dentro de etiquetas <resultado>.

<texto>
{texto}
</texto>";

        var prompts = new[] 
        { 
            ("Vago", promptVago), 
            ("Específico", promptEspecifico), 
            ("Con XML Tags", promptXmlTags) 
        };

        foreach (var (etiqueta, prompt) in prompts)
        {
            Console.WriteLine($"─── Prompt {etiqueta} ───");
            Console.WriteLine($"[Prompt enviado]: {prompt}\n");

            var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model = AnthropicModels.Claude46Sonnet,
                MaxTokens = 1024,
                Messages = new List<Message> { new Message(RoleType.User, prompt) }
            });

            Console.WriteLine($"[Respuesta de Claude]:\n{response.Message.ToString()}");
            Console.WriteLine();
        }

        Console.WriteLine("=== FIN SESION 3 ===");
    }
}
