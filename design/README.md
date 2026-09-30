# Inventory Pro — dizayn paketi

Claude Code-a göndəriləcək qovluq. Reponun `design/` qovluğuna kopyalayın.

| Fayl | Nə üçündür |
|---|---|
| `CLAUDE_CODE_PROMPT.md` | Claude Code-a yazılacaq mətn (kopyala-yapışdır) |
| `CLAUDE_CODE_HANDOFF.md` | Qaydalar, ekran → Razor view xəritəsi, Chart.js konfiqurasiyası, yeni .resx açarları, iş ardıcıllığı |
| `tokens.css` | İşıqlı + qaranlıq tokenlər, Bootstrap 5.3 dəyişənlərinə map. Olduğu kimi `wwwroot/css/` -ə |
| `ip-components.css` | `.ip-*` komponent sinifləri (rail, toolbar, düymə, sahə, cədvəl, nişan, tab, KPI, şəkil seçici, modal, toast, boş vəziyyət, skeleton, login). Olduğu kimi `wwwroot/css/` -ə |
| `screens/` | Hər ekranın referans şəkli. `01-*` qaranlıq, `02-*` işıqlı (design-system və edit/transfer üçün əlavə kadrlar) |

Yükləmə sırası `_Layout.cshtml`-də: `bootstrap.min.css` → `tokens.css` → `ip-components.css` → `site.css`.

Şəkillər 900px eninə çəkilib, ona görə cədvəllər üfüqi sürüşür; masaüstündə (1366+) hamısı bir ekrana sığır.
