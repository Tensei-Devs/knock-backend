using Xunit;

// Las pruebas de registros redirigen Console.Out (global al proceso) y los hosts comparten contenedores: sin paralelismo no se mezclan salidas.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
