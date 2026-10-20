# Keep the OpenAI-compatible endpoint for Anthropic Digest summaries, bounded by a 30-second timeout and one retry

Digest summaries and author intentions call Anthropic through its OpenAI-compatible chat-completions endpoint (`Llm:BaseUrl=https://api.anthropic.com/v1/`), using `ChatCompletionsSummaryGenerator`. Anthropic's docs say this layer "is primarily intended to test and compare model capabilities, and is not considered a long-term or production-ready solution for most use cases", while also saying it is "intended to remain fully functional and not have breaking changes". We keep it, rather than adding a gateway or a native Messages API generator, because:

- We use only the core path: one system message, one user message, `model`, text reply. The limitations the docs list (ignored `response_format`, `strict`, `logprobs`; no prompt caching; hoisted system messages) do not touch it. We send no `temperature` or `top_p`, which newer Claude models reject at some values.
- A summary is additive. A failed call already degrades to a Digest without a summary, so the endpoint's weaker production guarantee puts nothing critical at risk.
- It keeps the vendor a settings change (`Llm:BaseUrl`/`Llm:Model`/`Llm:ApiKey`), so moving to a gateway such as OpenRouter later needs no code.

The call is bounded in `LlmHttpClientExtensions.AddLlmHttpClient`: each attempt times out at 30 seconds and a failed attempt (timeout, 5xx, 408, 429, network error) is retried at most once, 2 seconds later. One call therefore takes about 62 seconds at worst. A large batch makes several calls (chunks run 5 at a time, then one combining call), so a fully hung provider can hold a search query's poll for a few minutes; one failed chunk fails the whole summary, which then ships without one. The same client serves author-intention calls.

## Consequences

- Revisit this decision if Anthropic deprecates or changes the compatibility layer, if summaries show quality or reliability problems the native API would fix, or if we need native-only features. The fallback is a second `ISummaryGenerator` that calls `POST /v1/messages` directly (`x-api-key` and `anthropic-version` headers, `max_tokens` set), chosen in `Program.cs` by a new setting.
- A live run of `ChatCompletionsSummaryGeneratorManualTest` against the configured model is the check that the endpoint still behaves; it is not part of the automated suite.

## Provider terms for the privacy policy

Source: Anthropic's privacy centre articles on data retention and model training for commercial products, read 2026-10-20. Check them again before the privacy policy is published; they change.

- **Retention:** Anthropic automatically deletes inputs and outputs from its backend within 30 days of receipt or generation. Exceptions: a zero-data-retention agreement, longer-lived services such as the Files API (not used), legal compliance, and Usage Policy enforcement.
- **Flagged content:** inputs and outputs flagged by an automated trust-and-safety system as a Usage Policy violation are kept for up to 2 years, and the classification scores for up to 7 years.
- **Training:** by default Anthropic does not use inputs or outputs from its commercial products to train models. Exceptions: feedback or bug reports the customer explicitly submits, and data the customer opts in to share. We do neither.
- **What we send:** record titles and abstracts from public literature databases. No user email, name or account data is sent to the provider.
