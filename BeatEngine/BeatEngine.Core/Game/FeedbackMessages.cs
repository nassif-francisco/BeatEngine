using System;

namespace BeatEngine.Core.Game
{
    // Add new phrases here; one is chosen each time an attempt finishes.
    internal static class FeedbackMessages
    {
        private static readonly string[] Success =
        {
            "¡Bien hecho!",
            "¡Brillante!",
            "¡Así se hace!",
            "Tus padres deben estar orgullosos",
            "¡Mente rápida!",
            "¡Excelente trabajo!",
            "¡Lo lograste!",
            "¡Qué gran idea!",
            "¡Sigue brillando!",
            "¡Imparable!",
            "¡Cada vez mejor!",
            "¡Tienes talento!"
        };

        private static readonly string[] Failure =
        {
            "Puedes hacerlo mejor",
            "Sigue intentándolo",
            "¡Tú puedes!",
            "Prueba otra vez",
            "¡No te rindas!",
            "Cada intento cuenta",
            "Vamos, un intento más",
            "Tómate tu tiempo",
            "Estás aprendiendo",
            "Revisa las sílabas y prueba de nuevo"
        };

        public static string NextSuccess() => Success[Random.Shared.Next(Success.Length)];
        public static string NextFailure() => Failure[Random.Shared.Next(Failure.Length)];
    }
}
