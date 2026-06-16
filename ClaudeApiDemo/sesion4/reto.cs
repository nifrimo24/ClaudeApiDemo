using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Tool = Anthropic.SDK.Common.Tool;

public static class RetoSesion4
{
    private const string SystemPrompt = """
        Eres el asistente interno de SIMED. Antes de responder preguntas sobre permisos
        o acciones administrativas, identifica siempre al usuario actual usando el tool
        get_current_user.

        REGLAS DE PERMISOS POR ROL:
        - Administrador: acceso total. Puede aprobar cualquier cancelación y otorgar
          incrementos de crédito sin límite.
        - Supervisor: puede aprobar cancelaciones de pedidos en estado "En proceso" y
          autorizar incrementos temporales de crédito (máx. 30 días).
        - Agente de soporte: solo puede cancelar pedidos en estado "Pendiente". No puede
          aprobar pedidos "En proceso" ni modificar límites de crédito.
        - Cliente: solo puede consultar el estado de sus propios pedidos.

        Responde en español, de forma breve y citando el rol del usuario actual.
        """;

    private static string GetCurrentUser() => JsonSerializer.Serialize(new
    {
        nombre = "Lucía Reyes",
        rol = "Supervisor"
    });

    public static async Task Ejecutar(AnthropicClient client)
    {
        Console.WriteLine("=== RETO 4: Tool real — get_current_user ===\n");

        var toolUsuarioActual = new Function(
            "get_current_user",
            "Returns the name and role of the user currently logged into the SIMED system. " +
            "Call this whenever you need to know who is making the request or what permissions they have " +
            "before answering questions about administrative actions, approvals or limits.",
            JsonNode.Parse("""{"type":"object","properties":{},"required":[]}""")
        );

        var systemMessages = new List<SystemMessage> { new SystemMessage(SystemPrompt) };

        var preguntas = new[]
        {
            "¿Tengo permisos para aprobar la cancelación del pedido ORD-002, que está 'En proceso'?",
            "¿Puedo otorgar un incremento temporal de crédito a un cliente?"
        };

        foreach (var pregunta in preguntas)
        {
            Console.WriteLine($"─── Usuario: {pregunta}");

            var messages = new List<Message> { new Message(RoleType.User, pregunta) };

            var response1 = await client.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model = AnthropicModels.Claude46Sonnet,
                MaxTokens = 512,
                System = systemMessages,
                Messages = messages,
                Tools = new List<Tool> { toolUsuarioActual }
            });

            Console.WriteLine($"    StopReason: {response1.StopReason}");
            foreach (var block in response1.Content)
                Console.WriteLine($"    → bloque: {block.GetType().Name}");

            if (response1.StopReason == "tool_use")
            {
                var toolCall = response1.Content.OfType<ToolUseContent>().First();
                Console.WriteLine($"    [Tool call → {toolCall.Name}() | id: {toolCall.Id}]");

                var usuarioJson = GetCurrentUser();
                Console.WriteLine($"    [Resultado de la función → {usuarioJson}]");

                messages.Add(response1.Message);
                messages.Add(new Message
                {
                    Role = RoleType.User,
                    Content = new List<ContentBase>
                    {
                        new ToolResultContent
                        {
                            ToolUseId = toolCall.Id,
                            Content = new List<ContentBase>
                            {
                                new TextContent { Text = usuarioJson }
                            }
                        }
                    }
                });

                var response2 = await client.Messages.GetClaudeMessageAsync(new MessageParameters
                {
                    Model = AnthropicModels.Claude46Sonnet,
                    MaxTokens = 512,
                    System = systemMessages,
                    Messages = messages,
                    Tools = new List<Tool> { toolUsuarioActual }
                });

                Console.WriteLine($"    Respuesta final: {response2.Message}");
                Console.WriteLine($"    StopReason final: {response2.StopReason}\n");
            }
            else
            {
                Console.WriteLine($"    Asistente (sin tool): {response1.Message}\n");
            }
        }
    }
}
