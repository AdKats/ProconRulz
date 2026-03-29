# Changelog

## Procon v2 Refactor

- Merge ProconRulzNL enhancements (LCARSx64): newline escape (`\n`) support in yell messages
- Split monolithic source into partial classes: Settings, Events, RuleParser, RuleExecutor, Actions
- Convert encoding from UTF-16LE to UTF-8 with LF line endings
- Move source to `src/` directory with subfolder layout
- Convert code style to System types (String, Int32, Boolean, etc.)
- Add .editorconfig for code style enforcement
- Add ProconRulz.csproj for CI format checks
- Add CI and release GitHub Actions workflows
- Add CLAUDE.md, README.md, CHANGELOG.md, .gitignore

## v44j.1 (Legacy)

- Support for BF4
- Support for rulz files (Plugins/BF4/proconrulz_*.txt)
- 'Not' modifier for If and Text conditions
- VictimTeamKey substitution variable
- Enable/Disable rulz.txt files in settings
- Linux support for external rulz files
- On Init and On RoundOver triggers
- Reload .txt rulz files on plugin enable

See release notes in source code for full history of versions v19-v44.
