# GitHub Pages Custom Domain Configuration

Status: active and verified on 2026-07-17.

Recommended hostname:

```text
nexus.mouadh.org
```

## Why This Hostname

- `.org` matches an open research-infrastructure and community project better
  than `.shop`.
- A `nexus` subdomain preserves `mouadh.org` for the owner's broader public
  identity.
- A subdomain uses one stable DNS `CNAME` record instead of binding the apex
  domain to GitHub Pages IP addresses.

`mouadh.info` remains a good personal-profile or documentation domain.
`mouadh.shop` should be reserved for a commercial storefront if one is needed.

## Active Configuration

The repository is owned by the `nexus-scholar-org` GitHub organization. Domain
verification therefore belongs at the organization level. The current
configuration is:

1. The `mouadh.org` domain is verified in the `nexus-scholar-org`
   organization.
2. The organization-verification TXT record remains in DNS.
3. This repository's **Settings → Pages** custom domain is
   `nexus.mouadh.org`.
4. The DNS provider must expose:

   ```text
   Type:   CNAME
   Name:   nexus
   Target: nexus-scholar-org.github.io
   ```

   The target must not include `/core-csharp`.
5. Verify with PowerShell:

   ```powershell
   Resolve-DnsName nexus.mouadh.org -Type CNAME
   ```

6. GitHub Pages reports the protected domain as verified, the deployment as
   built, and **Enforce HTTPS** as enabled.
7. Canonical, Open Graph, robots, and sitemap URLs in `site/` use the custom
   hostname.

## Actions-Workflow Detail

This site deploys with `.github/workflows/pages.yml` and
`actions/deploy-pages`. GitHub's custom-workflow Pages mode does not require a
checked-in `site/CNAME`; GitHub ignores that file in this mode. The custom
domain must be configured in repository settings.

## Safety Rules

- Do not configure DNS before adding and verifying the domain in GitHub.
- Do not use wildcard DNS records for GitHub Pages.
- Do not remove the organization-verification TXT record after verification.
- Do not claim the custom domain is active until DNS, TLS, redirects, and the
  Pages deployment have been checked from outside the local network.

## Canonical Public Site

```text
https://nexus.mouadh.org/
```

The organization project URL redirects to the custom hostname:

```text
https://nexus-scholar-org.github.io/core-csharp/
```

## GitHub References

- [Manage a custom domain for a GitHub Pages site](https://docs.github.com/en/pages/configuring-a-custom-domain-for-your-github-pages-site/managing-a-custom-domain-for-your-github-pages-site)
- [Verify a custom domain for GitHub Pages](https://docs.github.com/en/enterprise-cloud@latest/pages/configuring-a-custom-domain-for-your-github-pages-site/verifying-your-custom-domain-for-github-pages)
- [Secure a GitHub Pages site with HTTPS](https://docs.github.com/en/pages/getting-started-with-github-pages/securing-your-github-pages-site-with-https)
