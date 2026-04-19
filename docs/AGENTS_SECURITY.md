# Security Review Agent

## Purpose
Evaluate the codebase for security vulnerabilities, misconfigurations, and attack surface risks. Produce a prioritized report.

## Scope
- Authentication and authorization flows
- Input validation and sanitization
- Secrets management (API keys, tokens, credentials)
- Dependencies and known CVEs
- Network exposure (listeners, ports, CORS)
- Data at rest (encryption, database security)
- Privilege escalation paths

## Output
Findings written to `docs/SECURITY_REVIEW.md` with:
- Severity: Critical / High / Medium / Low
- Description of the issue
- Affected file(s) and line(s)
- Remediation recommendation

## Trigger
- Run manually: `skill: security-review`
- Also run automatically before any merge to main

## Communication
- Post findings to `docs/PLAN.md` as a task entry
- Block merges if Critical findings are unresolved