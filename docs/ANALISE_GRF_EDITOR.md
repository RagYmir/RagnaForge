# Analise tecnica do GRF Editor

Data: 2026-05-06
Status: Discovery inicial
Caminho analisado: `<GRF_EDITOR_PATH>`

## Metodo

Analise estrutural segura, sem descompilacao e sem copia permanente de codigo.

Foram usados:

- Inventario de arquivos.
- Metadados de versao.
- Configs `.exe.config`.
- Metadata .NET por reflection.
- Nomes de recursos embutidos.
- Strings tecnicas filtradas dos binarios.
- Execucao limitada do `GrfCL.exe` sem argumentos e com flags de ajuda para observar comportamento.

Nao foi feito:

- Descompilacao de codigo.
- Copia de codigo-fonte.
- Edicao em arquivos do GRF Editor.
- Extracao permanente de DLLs.
- Reuso de assemblies sem decisao/licenca.

## Verificacao complementar de origem publica

Em 2026-05-07 foi verificada a origem publica do projeto:

- Repositorio publico encontrado: `https://github.com/Tokeiburu/GRFEditor`
- O codigo-fonte publico confirma a existencia dos projetos `GRF`, `GrfCL`, `ActImaging`, `GrfToWpfBridge` e `GRFEditor`.
- A API publica do GitHub para licenca do repositorio retornou `404` e o campo `license` do repositorio veio `null` no momento da verificacao.

Conclusao pratica:

- O projeto esta publicamente disponivel para estudo tecnico.
- Isso nao equivale, por si so, a permissao clara para copiar ou redistribuir codigo/assemblies.
- Nossa politica local passa a ser: usar como referencia tecnica e integrar por wrapper/processo externo; incorporacao direta de codigo fica bloqueada ate licenca explicita ou permissao do autor.

Atualizacao posterior:

- Em 2026-05-07 o usuario informou que obteve autorizacao direta do criador do GRF Editor para uso da base tecnica e de DLLs necessarias.
- Com isso, a restricao de incorporacao tecnica deixa de ser bloqueio de projeto e a trilha de `GrfEditorAssemblyEngine` passa a ser viavel.

## Estrutura encontrada

```text
<GRF_EDITOR_PATH>\
  app.ico
  GRF Editor.exe
  GRF Editor.exe.config
  GrfCL.exe
  GrfCL.exe.config
  unins000.dat
  unins000.exe
```

Nao ha diretorios de fonte ou DLLs soltas no caminho instalado. As bibliotecas relevantes estao embutidas como recursos nos executaveis.

## Executaveis

| Arquivo | Produto | Versao | Tamanho | Stack observada |
| --- | --- | --- | ---: | --- |
| `GRF Editor.exe` | GRFEditor | 1.5.3.3063 | 5,037,056 bytes | .NET Framework/WPF, MSIL |
| `GrfCL.exe` | GrfCL | 1.0.2.1373 | 1,982,976 bytes | .NET Framework console, MSIL |
| `unins000.exe` | Instalador/uninstaller | n/a | 3,218,493 bytes | Inno/installer |

Configs dos executaveis declaram runtime .NET `v4.0` e fallback `v2.0.50727`.

## Referencias .NET observadas

`GRF Editor.exe` referencia:

- `GRF`
- `ActImaging`
- `GrfToWpfBridge`
- `TokeiLibrary`
- `Utilities`
- `Lua`
- `Encryption`
- `ErrorManager`
- `ICSharpCode.AvalonEdit`
- `OpenTK`
- `OpenTK.GLControl`
- `System.Windows.Forms`
- `PresentationCore`
- `PresentationFramework`
- `WindowsBase`
- `WindowsFormsIntegration`

`GrfCL.exe` referencia:

- `GRF`
- `ActImaging`
- `GrfToWpfBridge`
- `Utilities`
- `Encryption`
- `ErrorManager`
- `TokeiLibrary`

## Bibliotecas embutidas encontradas

No `GRF Editor.exe` foram encontrados recursos de DLL, alguns compactados em GZip:

- `GRF.dll`
- `ActImaging.dll`
- `GrfToWpfBridge.dll`
- `TokeiLibrary.dll`
- `Utilities.dll`
- `Lua.dll`
- `Encryption.dll`
- `ErrorManager.dll`
- `ICSharpCode.AvalonEdit.dll`
- `OpenTK.dll`
- `OpenTK.GLControl.dll`
- `Be.Windows.Forms.HexBox.dll`
- `ColorPicker.dll`
- `Gif.Components.dll`
- `GrfMenuHandler32.dll`
- `GrfMenuHandler64.dll`
- `cps.dll`

Metadados internos indicam caminhos de build como `C:\tktoolsuite\GRFEditor\GRF\obj\Release\netstandard2.0\GRF.pdb` e `Utilities\obj\Release\netstandard2.0`, sugerindo que partes centrais podem ter sido compiladas como `netstandard2.0`.

## Formatos suportados inferidos

Pelos recursos, referencias e strings tecnicas, o GRF Editor instalado suporta ou conhece:

- Containers: `.grf`, `.gpf`, `.thor`, `.rgz`
- Sprites/animacoes: `.spr`, `.act`, `.pal`
- Mapas/modelos: `.rsw`, `.gnd`, `.gat`, `.rsm`, `.rsm2`
- Efeitos: `.str`
- Imagens: `.bmp`, `.png`, `.jpg`, `.tga`, `.ico`, `.gif`
- Scripts/client: `.lua`, `.lub`, `.xml`, `.txt`, `.ini`
- Compressao/encriptacao: zlib/deflate, LZMA, AES/encryption flags, GRF key

Namespaces/strings relevantes observados:

- `GRF.ContainerFormat`
- `GRF.FileFormats.ActFormat`
- `GRF.FileFormats.SprFormat`
- `GRF.FileFormats.GatFormat`
- `GRF.FileFormats.GndFormat`
- `GRF.FileFormats.RswFormat`
- `GRF.FileFormats.RsmFormat`
- `GRF.FileFormats.StrFormat`
- `GRF.FileFormats.ThorFormat`
- `GRF.FileFormats.LubFormat`
- `GRF.FileFormats.TgaFormat`
- `GRF.Core.GroupedGrf`
- `GRF.Image`
- `GRF.Graphics`
- `GatPreviewImageMaker`
- `GrfToWpfBridge.MultiGrf.MetaGrfResourcesViewer`
- `ActImaging.WpfImaging`

## Componentes visuais e ferramentas internas inferidas

O `GRF Editor.exe` contem XAML/strings para:

- `PreviewSprites`
- `PreviewAct`
- `PreviewRsm`
- `PreviewStr`
- `PreviewImage`
- `PreviewText`
- `PreviewWav`
- `PreviewResource`
- `TypeExplorer`
- `GrfClusterView`
- `OpenGLViewport`
- `MapExtractor`
- `MapEditorWindow`
- `SpriteEditorTab`
- `SpriteConverter`
- `ValidationDialog`
- `PreviewResourceIndexer`

Tambem contem shaders OpenGL para mapa/modelos:

- `map.gnd`
- `map.rsm`
- `map.water`
- `map.lub`
- `map.gat`
- `map.skymap`
- `str.str`

## GrfCL.exe

O `GrfCL.exe` foi identificado como ferramenta de linha de comando para gerenciar GRF.

Comportamento observado:

- Sem argumentos: retorna erro dizendo que foi aberto sem argumentos.
- `--help`: retorna `Command unrecognized --help`.
- `/?`: retorna `Command unrecognized /?`.

Strings internas indicam comandos/operacoes como:

- open/save
- extract
- fast extraction
- merge GRF
- patch/Thor
- delete
- compression
- encryption/decryption
- repack/compact

Conclusao: o CLI provavelmente e reutilizavel, mas exige descoberta da sintaxe real por testes controlados com GRFs temporarios de amostra ou por documentacao/codigo-fonte externo, se autorizado.

## Investigacao controlada do GrfCL

Em 2026-05-07 foi feita uma validacao controlada com:

- codigo-fonte publico clonado temporariamente do repositorio `Tokeiburu/GRFEditor`, commit `dfa26ab`;
- um GRF temporario de laboratorio criado fora do workspace principal;
- nenhum arquivo do servidor, Patch/client ou GRFs reais foi alterado.

Comandos confirmados pelo codigo-fonte publico:

- `-version`
- `-open`
- `-grfInfo`
- `-extractGrf`
- `-makeGrf`

Achados do laboratorio:

- `-version` funciona de forma limpa e imprime a versao.
- `-open <grf> -grfInfo` funciona e imprime metadados do container.
- `-makeGrf` criou um GRF temporario valido.
- `-extractGrf` extraiu corretamente o conteudo do GRF temporario.
- Em PowerShell, alguns comandos emitiram `#Exception : Identificador invalido.` mesmo quando o efeito principal aconteceu com sucesso.

Implicacoes para a integracao:

- O wrapper nao pode confiar apenas no exit code do processo.
- O wrapper tambem nao pode tratar qualquer linha `#Exception` como falha terminal sem verificar side effects.
- Para operacoes de laboratorio e inspecao, o criterio de sucesso precisa combinar:
  - saida do processo;
  - existencia/consistencia dos artefatos esperados;
  - contexto do comando executado.
- A indexacao de conteudo interno de GRF deve ser opt-in e por container, nao repo-wide, ate o adapter ficar resiliente.

## Possiveis formas de reaproveitamento

### Opcao A: wrapper de processo sobre `GrfCL.exe`

Vantagens:

- Menor risco legal e tecnico.
- Nao carrega assemblies internos no nosso processo.
- Mantem o GRF Editor como dependencia externa instalada.
- Bom para extrair/listar/gerar containers se a sintaxe for descoberta.

Limites:

- Ajuda CLI nao esta exposta por `--help`.
- Pode ser dificil obter preview de sprites/mapas.
- Erros precisam de parser resiliente.

### Opcao B: adaptador .NET carregando assemblies do GRF Editor

Vantagens:

- Melhor acesso a `GRF.dll`, `ActImaging`, `GrfToWpfBridge`.
- Possivel reutilizacao de leitura de GRF/Thor, formats e sprites.
- Alinha o core do projeto ao ecossistema do GRF Editor.

Limites:

- Assemblies estao embutidos no exe, nao distribuidos como pacote.
- Reuso direto continua condicionado a licenca explicita ou permissao do autor.
- Precisa isolar em `GrfEditorIntegration` para evitar acoplamento.
- Compatibilidade entre .NET Framework 4.x, netstandard2.0 e .NET moderno precisa de spike tecnico.

### Opcao C: usar GRF Editor como referencia e implementar adapter proprio

Vantagens:

- Controle total.
- Menor dependencia operacional do binario.

Limites:

- Recriar parser/renderizador de GRF/map/sprite e caro e arriscado.
- Contraria a diretriz de nao reescrever sem necessidade.

## Recomendacao inicial

Adotar uma arquitetura .NET/C# como nucleo, com camada adaptadora versionavel:

```text
GrfEditorIntegration
  GrfEditorInstallationProbe
  GrfCliAdapter
  GrfAssemblyProbe
  GrfCapabilities

Grf
  IGrfEngine
  IGrfScanner
  IAssetPreviewProvider
  GrfAssetIndex
```

Fase 1:

- Nao referenciar diretamente DLL embutida no produto.
- Criar probe de instalacao e capability matrix.
- Testar `GrfCL.exe` com arquivos GRF temporarios de teste.
- Separar comandos CLI em adapter.

Fase 2:

- Reavaliar licenca/permissao do GRF Editor.
- Se houver permissao explicita, avaliar referencia direta a `GRF.dll`/`ActImaging.dll`.
- Caso positivo, encapsular tudo em projeto separado `GrfEditorIntegration`.

Estado atual:

- O usuario informou permissao direta do criador.
- A proxima etapa tecnica recomendada passa a ser um spike controlado de carregamento de `GRF.dll` e listagem de conteudo interno de um GRF de laboratorio dentro de `GrfEditorIntegration`.

## Spike de integracao direta por assembly

Em 2026-05-07 o spike foi implementado no workspace do projeto, sem copiar DLLs para dentro do repositorio:

- `GrfEditorAssemblyLoadContext`
- `GrfAssemblyContainerInspector`
- comando CLI `grf inspect`

Abordagem adotada:

- usar `GrfCL.exe` como host;
- carregar `GRF.dll` e dependencias diretamente dos recursos embutidos;
- manter o carregamento isolado em `AssemblyLoadContext` descartavel;
- salvar somente um indice JSON local em `data/indexes/`.

Resultado validado:

- leitura direta de um GRF real (`data_0.grf`) funcionou;
- o motor reportou `131185` entradas e `2802` diretorios no container inspecionado;
- contagens por extensao foram obtidas com sucesso para `.bmp`, `.act`, `.spr`, `.tga`, `.rsm`, `.gat`, `.gnd`, `.rsw`, `.lub` e outras;
- a captura de amostra foi limitada por `--limit`, mantendo o comando seguro para uso exploratorio inicial.

Conclusao pratica:

- a trilha de integracao direta com `GRF.dll` esta tecnicamente viavel neste ambiente;
- nao dependemos mais apenas do wrapper de processo para introspeccao de conteudo interno;
- o proximo ganho vem de conectar esse indice/lookup ao resolver de dependencias de item/equipamento/mapa.

Fase 3:

- Preview visual pode ser WPF/WebView/Avalonia, mas deve consumir interfaces do core, nao acessar GRF Editor diretamente.

## Riscos

- O repositorio publico do GRF Editor nao expunha licenca detectada no momento da verificacao.
- DLLs embutidas nao devem ser copiadas para o projeto sem permissao clara ou licenca explicita.
- `GRF Editor.exe` e aplicativo GUI; executar com flags desconhecidas pode abrir janela.
- `GrfCL.exe` nao documenta ajuda pelo padrao comum.
- `GrfCL.exe` pode emitir excecoes textuais ruidosas mesmo quando a operacao principal foi concluida.
- Formats de client Ragnarok variam fortemente por data e regioes.
- Map preview/render usa OpenTK/OpenGL; reaproveitamento visual pode exigir Windows Desktop runtime.

## Decisao preliminar

O GRF Editor deve ser tratado como fundacao tecnica para GRF/asset scanning, mas por meio de adapter isolado. A stack principal nao deve ser FastAPI + React por padrao; a recomendacao inicial e core .NET/C#, com API .NET e UI decidida depois do scanner/dry-run.
