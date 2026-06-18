using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

public class RetoSesion4
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== SESION 4: RETO SEMILLERO - Routing de consultas ===\n");
        var client = new AnthropicClient(); 

        var consultas = new[]
        {
            "¿Dónde está mi paquete? Debería haber llegado ayer.",
            "La pantalla de la aplicación se queda en blanco cuando intento iniciar sesión.",
            "Quiero devolver unos zapatos que compré, me quedaron pequeños.",
            "¿Tienen descuento por el Black Friday?", // Caso borde / Out of domain
            "El repartidor dejó el paquete en la casa del vecino."
        };

        var routerSystemPrompt = new List<SystemMessage>
        {
            new SystemMessage(@"Eres un clasificador de consultas para soporte al cliente de un E-commerce.
Clasifica la consulta del usuario en una de las siguientes 3 categorías exactas:
- 'ESTADO_PEDIDO' (preguntas sobre envíos, entregas, tracking)
- 'SOPORTE_TECNICO' (problemas con la app, web, errores del sistema)
- 'DEVOLUCIONES' (reembolsos, cambios, devoluciones de producto)

Si la consulta no encaja en ninguna, responde 'OTRO'.
Tu respuesta debe ser UNICAMENTE el nombre de la categoría, sin texto adicional.")
        };

        var promptsPorCategoria = new Dictionary<string, string>
        {
            { "ESTADO_PEDIDO", "Eres un asistente de envíos. Pide el número de rastreo amablemente e indica que revisarás el estado en el sistema logístico." },
            { "SOPORTE_TECNICO", "Eres un agente de soporte técnico Nivel 1. Pide al usuario que describa el dispositivo que usa (iOS/Android/Web) y sugiere limpiar caché." },
            { "DEVOLUCIONES", "Eres el encargado de devoluciones. Indica que el plazo máximo es de 30 días y que necesitas el número de orden para generar la etiqueta." },
            { "OTRO", "Eres un asistente general. Indica que no puedes resolver esa consulta pero que lo transferirás a un humano." }
        };

        foreach (var consulta in consultas)
        {
            Console.WriteLine($"[Consulta]: {consulta}");
            
            // 1. Clasificar con Haiku
            var routerResponse = await client.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model = AnthropicModels.Claude45Haiku,
                MaxTokens = 50,
                System = routerSystemPrompt,
                Messages = new List<Message> { new Message(RoleType.User, consulta) }
            });

            var categoria = routerResponse.Message.ToString().Trim();
            Console.WriteLine($"[Clasificación (Haiku)]: {categoria}");

            if (!promptsPorCategoria.ContainsKey(categoria))
            {
                categoria = "OTRO"; // Fallback por si alucina otra cosa
            }

            // 2. Responder con Sonnet usando el system prompt específico
            var systemPromptEspecifico = new List<SystemMessage> { new SystemMessage(promptsPorCategoria[categoria]) };
            var finalResponse = await client.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model = AnthropicModels.Claude46Sonnet,
                MaxTokens = 500,
                System = systemPromptEspecifico,
                Messages = new List<Message> { new Message(RoleType.User, consulta) }
            });

            Console.WriteLine($"[Respuesta Final (Sonnet)]:\n{finalResponse.Message.ToString()}");
            Console.WriteLine(new string('-', 50));
        }

        Console.WriteLine("\n[Documentación de Clasificación Incorrecta]:");
        Console.WriteLine("Si la consulta '¿Tienen descuento por el Black Friday?' fue clasificada como algo distinto a 'OTRO', se considera incorrecta.");
        Console.WriteLine("Si alguna otra fue mal clasificada, puedes notarlo arriba.");
        Console.WriteLine("=== FIN SESION 4 ===");
    }
}
