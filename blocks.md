# blocks.txt

Outros programas podem criar ou atualizar `%USERPROFILE%\blocks.txt` (`~/blocks.txt`) para definir bloqueios por **título original da janela**, sem considerar aliases ou processos.

## Formato

- Texto UTF-8, com ou sem BOM; quebras de linha CRLF ou LF.
- Uma regex C#/.NET Framework 4.8 por linha, sem delimitadores `/.../`, aspas ou escape de string C#.
- Qualquer expressão correspondente bloqueia o título. Por padrão, diferencia maiúsculas de minúsculas e pode corresponder a apenas parte do título. Use `(?i)` para ignorar caixa e `^...$` para exigir o título inteiro.
- Linhas vazias ou só com espaços são ignoradas. Nas demais, espaços fazem parte da regex. Não há sintaxe de comentários.
- Regex inválidas são ignoradas individualmente. Expressões que excedam 100 ms de avaliação bloqueiam aquela tentativa por segurança.

Exemplo de conteúdo:

```text
(?i)youtube
^Documento confidencial - Bloco de Notas$
(?i)\b(netflix|twitch)\b
```

## Escrita e atualização

Crie o arquivo caso não exista. Para atualizar, escreva a lista completa em um arquivo temporário na mesma pasta, feche-o e substitua `blocks.txt` atomicamente (por exemplo, `File.Replace` quando o destino já existe). Evite apagar o destino antes da substituição ou deixá-lo parcialmente escrito. Se houver vários escritores, coordene as atualizações para não perder regras de outro programa.

Criação, alteração, renomeação/substituição e exclusão são detectadas automaticamente, com uma breve espera de aproximadamente 100 ms para agrupar eventos. Não é necessário reiniciar. As regras são aplicadas na próxima listagem de janelas; o estado das janelas já listadas permanece fixo durante aquele ciclo de seleção. Falhas temporárias de leitura preservam a última lista carregada e provocam novas tentativas.

Arquivo vazio ou ausente significa nenhum bloqueio na próxima listagem. Remover uma regex libera os títulos correspondentes nesse próximo ciclo, desde que nenhuma outra regra os bloqueie. As regras não fecham nem minimizam automaticamente janelas já abertas.
