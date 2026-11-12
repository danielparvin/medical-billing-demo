# Agent evals

Small, repeatable experiments that give the same prompt to several fresh agents and grade what they did with tests the
agents never see. The results so far, and what they mean for the talk, are in the
[Agent evals](../README.md#agent-evals) section of the project README.

| Eval | Prompt | Copy of the repo | Hidden grader |
|---|---|---|---|
| `rounding` | [rounding.md](prompts/rounding.md) | As committed, minus talk material | [RoundOnceGrader.cs](graders/RoundOnceGrader.cs) |
| `rounding-no-rule` | [rounding-no-rule.md](prompts/rounding-no-rule.md) | Also without the written round-once rule | [RoundOnceGrader.cs](graders/RoundOnceGrader.cs) |
| `discount` | [discount.md](prompts/discount.md) | As committed, minus talk material | [DiscountGrader.cs](graders/DiscountGrader.cs) |

Every copy is a snapshot of commit `f52c5b8`, which predates the talk demos, with no git history, no tags, and no
checkpoints, talk arc or live-coding script in its README. Without that, an agent could find the finished discount in
the history at commit `58955b6`, or read that the discount contradicts a test.

## Run an eval
1. Make the copies (five per eval by default; `-Eval` and `-Runs` narrow it):
   ```powershell
   ./evals/prepare-evals.ps1 -OutDir C:\temp\evals
   ```
2. In each folder, start a fresh agent session with no memory of this repo and paste the prompt for its eval from
   `prompts/`. Let it finish; don't answer its questions, so every run gets the same input.
3. Grade every copy:
   ```powershell
   ./evals/grade-evals.ps1 -OutDir C:\temp\evals
   ```
4. Read each agent's final report as well. The table can't tell you whether an agent explained a change or asked first.

The grading table shows, for each copy, how many files changed, whether the engine changed, what happened to the test
the discount contradicts, whether the agent's own domain and web tests pass, and how many hidden checks pass. The
integration tests are not run.

## What the graders were checked against
On untouched copies, `RoundOnceGrader` passes and `DiscountGrader` passes 2 of 6 (the unpriced-line and cap checks also
hold without a discount). With the rounding bug from `scripts/demo-rounding-bug.ps1`, `RoundOnceGrader` fails. With the
engine from commit `58955b6`, `DiscountGrader` passes 6 of 6.

## Limits
- Five runs per eval are anecdotes, not rates. Agents and models change, so rerun the evals before quoting them.
- The results in the project README came from Claude Code subagents started for each copy, not from your stage setup.
  Those runs prefixed the prompt with the copy's path, which these prompt files leave out.
- The copies drop talk material from the README, so a later change to `f52c5b8`'s README or docs could break
  `prepare-evals.ps1`. It stops with an error rather than making a copy that leaks the answer.
