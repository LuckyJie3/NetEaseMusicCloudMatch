# Security Policy

## Supported version

Security fixes are made against the latest code on the default branch. Older binaries and unofficial redistributions may not receive fixes.

## Reporting a vulnerability

Please use GitHub's **Report a vulnerability** / private security advisory feature for this repository. Do not open a public Issue when a report contains credentials, a reproducible account-security problem, or information that could expose another user.

Never include a real NetEase Cloud Music Cookie, `MUSIC_U`, `MUSIC_A`, `MUSIC_R_T`, `__csrf`, Authorization header, `session.dat`, or an unredacted application log. Replace secrets with `[REDACTED]` and include the CorrelationId when available.

A useful report contains:

- affected version or commit;
- Windows version;
- expected and actual behavior;
- minimal reproduction steps;
- sanitized logs or screenshots;
- an assessment of possible impact.

## Credential model

The application accepts only a Cookie pasted by the user. It does not read browser Cookie databases. A remembered session is protected with Windows DPAPI for the current Windows user, and network diagnostics are sanitized before they reach the logger.

The project maintainers will never ask a user to post or send a complete account Cookie.
