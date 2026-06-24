## 1. Source: Rename types and factory method

- [x] 1.1 Rename `MarkdownTokenizer.cs` to `WordTokenizer.cs`, rename class to `WordTokenizer`, update all doc comments and parameter name (`markdown` → `text`)
- [x] 1.2 In `IndexedDocument.cs`, rename `FromMarkdown` → `FromText`, update doc comments (`markdown` → `text`), update reference from `MarkdownTokenizer` → `WordTokenizer`
- [x] 1.3 In `SpanMatcher.cs`, update reference from `MarkdownTokenizer.Tokenize` → `WordTokenizer.Tokenize`
- [x] 1.4 In `SpanMatch.cs`, update doc comment: `Original markdown text` → `Original text`
- [x] 1.5 In `Token.cs`, update doc comment: `extracted from markdown text` → `extracted from text`

## 2. Tests: Rename test file and update all references

- [x] 2.1 Rename `MarkdownTokenizerTests.cs` to `WordTokenizerTests.cs`, rename class to `WordTokenizerTests`, update all `MarkdownTokenizer.Tokenize` → `WordTokenizer.Tokenize`, rename test methods (e.g., `BasicMarkdown_Tokenizes` → `BasicText_Tokenizes`, `MarkdownStructures_SplitOnSyntax` → `FormattingStructures_SplitOnSyntax`)
- [x] 2.2 In `IndexedDocumentTests.cs`, update all `FromMarkdown` → `FromText`, rename test methods (e.g., `FromMarkdown_BuildsCorrectly` → `FromText_BuildsCorrectly`)
- [x] 2.3 In `SpanMatcherTests.cs`, update all `FromMarkdown` → `FromText`
- [x] 2.4 In `EdgeCaseTests.cs`, update all `FromMarkdown` → `FromText`
- [x] 2.5 In `BenchmarkTests.cs`, update all `FromMarkdown` → `FromText`
- [x] 2.6 In `SpecValidationTests.cs`, update `FromMarkdown` → `FromText`, `MarkdownTokenizer` → `WordTokenizer`, rename test methods referencing markdown, update inline comment (`Query with markdown syntax` → `Query with formatting characters`)

## 3. Documentation

- [x] 3.1 Update `README.md`: change `FromMarkdown` → `FromText` in example code, update description comment

## 4. Verification

- [x] 4.1 Build solution (`dotnet build`) — confirm zero errors
- [x] 4.2 Run full test suite (`dotnet test`) — confirm all tests pass
- [x] 4.3 Run `grep -r "markdown" src/ tests/ README.md --include="*.cs" --include="*.md" | grep -v "/obj/" | grep -v "/bin/"` — confirm zero remaining references (except openspec/ and archived changes)
