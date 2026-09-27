# Populated alpha upgrade check

Run the upgrade check after building a candidate image:

```bash
docker build -f distribution/docker/Dockerfile -t komarr:upgrade-check .
python3 scripts/smoke-populated-upgrade.py --new-image komarr:upgrade-check
```

The script starts from the published `v0.1.0-alpha.1` amd64 image at digest
`sha256:1700f0a5fa7b201e3817689e4add7490645b86dcc2289cf110870d353e02d7db`.
It creates a fresh alpha database, adds synthetic manga, coverage, policy,
download and history records, a CBZ archive, a root folder, and a disabled
download client. It then boots the candidate image on the same `/config` and
library volumes and checks the records through the API. The candidate creates
a real backup ZIP, which the script restores into a separate fresh container.
An incomplete ZIP must fail without changing the target database or config.

CI runs the same check against the image built from each pull request. The
fixture uses synthetic data inserted into the alpha schema. It proves the
upgrade and recovery path for those records; it cannot represent every user
database or a live download-client session. The beta cohort task covers real
libraries and service combinations.
