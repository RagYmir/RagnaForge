# RagnaForge Code Style Rules

- Use .NET/C# como stack principal.
- Não criar Python/Node aleatoriamente se C# resolver.
- Manter compatibilidade com Windows: paths com espaços, acentos e apóstrofos.
- Usar `Path.GetFullPath()` para normalizar caminhos.
- Usar `StringComparison.OrdinalIgnoreCase` para comparações de path no Windows.
- Usar `System.Text.Json` para serialização JSON.
- Não usar Newtonsoft.Json salvo necessidade comprovada.
- Não usar `unsafe` code salvo necessidade comprovada.
- Manter testes unitários para toda lógica de segurança.
- Usar fixtures temporárias nos testes, não caminhos reais.
- Nunca hardcodar paths na lógica do código.
- Documentar decisões técnicas em `docs/`.
