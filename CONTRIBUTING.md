# Contributing

Contributions are welcome through issues and pull requests.

By submitting a contribution, you agree that it is licensed under Redmond
Calculator's [MIT License](LICENSE). You represent that you have the right to
submit the contribution under those terms.

Microsoft Calculator remains an independently licensed, pinned upstream
submodule. Do not submit changes inside `upstream/windows-calculator`; put
portable adaptations in this repository. Changes intended for
`redmond-commons` should be contributed to that separately licensed
repository.

Before opening a pull request, follow the build and test commands in
[AGENTS.md](AGENTS.md), run `scripts/verify-upstream-pristine.sh`, and run
`scripts/verify-licensing.sh`.
