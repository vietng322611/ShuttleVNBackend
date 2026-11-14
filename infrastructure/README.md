# ShuttleVN Infrastructure

The stack runs on AWS in `ap-southeast-1`, provisioned by Terraform and converged
by Ansible. Two EC2 hosts share one VPC:

| Host | Role | Deploys |
| :--- | :--- | :--- |
| `public_ec2` | Nginx edge, TLS termination, GitHub runner | Frontend (`shuttle-vn-frontend`) |
| `private_ec2` | API container, Prometheus/Grafana/Tempo | Backend (this repository) |

Only Nginx is reachable from the internet, and only from published Cloudflare IPv4
ranges. The API has no public address; the edge proxies `/api/` to it over the
private subnet.

The database is an external managed PostgreSQL instance. It is not part of the
Terraform state and survives a full destroy.

## Layout

Paths below are relative to the repository root.

```text
infrastructure/
|- terraform-bootstrap/   # S3 state bucket + GitHub OIDC deploy role (run once)
|- iam/                   # policy for the human bootstrap user
|- terraform-aws/         # VPC, hosts, security groups; S3 remote state
`- ansible/
   |- inventory/          # dynamic inventory from terraform output, group_vars
   |- playbooks/          # common, public, private, exporters, observability, runners
   `- templates/          # nginx, compose, Prometheus, Grafana, Tempo
```

## Prerequisites

- Terraform >= 1.10 (S3 native state locking) and AWS credentials that can create
  the bootstrap resources.
- Ansible with `community.docker` on the operator machine.
- An Ubuntu AMI ID for `ap-southeast-1` and an SSH key pair.
- A Cloudflare origin certificate for the API hostname, kept outside the repository.
- GitHub repository secrets and variables listed under [GitHub configuration](#github-configuration).

## 1. Bootstrap (once per AWS account)

`terraform-bootstrap` cannot use the S3 backend it creates, so it runs on local
state. It creates the state bucket and the IAM role that GitHub Actions assumes
through OIDC.

```bash
cd infrastructure/terraform-bootstrap
cp terraform.tfvars.example terraform.tfvars   # if not present
terraform init && terraform apply
```

Variables:

| Variable | Meaning |
| :--- | :--- |
| `github_repository` | `owner/repo` allowed to assume the deploy role. |
| `github_branch` | Branch whose pushes may assume the role (default `main`). |
| `github_environment` | Environment used by the approval-gated apply job (default `production`). |

The role trusts two `sub` claims: a push to `github_branch`, and a job running in
`github_environment`. Numeric GitHub IDs inside the claim are wildcarded; the owner
and repository names are not.

The bucket name is derived from the account ID. It must match the `backend "s3"`
block in `infrastructure/terraform-aws/versions.tf`; the bucket is not looked up at
runtime, so a mismatch fails at `terraform init`.

Re-apply the bootstrap whenever the trusted branch or environment changes.

## 2. Provision AWS

```bash
cd infrastructure/terraform-aws
cp terraform.tfvars.example terraform.tfvars
terraform init
terraform plan -out tfplan
terraform apply tfplan
```

| Variable | Meaning |
| :--- | :--- |
| `project_name` | Prefix for resource names and tags. |
| `ami_id` | Pinned Ubuntu amd64 AMI in Singapore. |
| `ssh_public_key` | Public key **contents**, not a path. |
| `ssh_allowed_cidr` | Administrator IPv4 address with `/32` for SSH to the public host. |
| `enable_nat_gateway` | `false` removes outbound internet from the private host (see [Cost control](#cost-control)). |

The public host has an Elastic IP so its address survives an instance replacement.
Cloudflare points at that address; recreating the instance without the EIP breaks
the edge silently.

`terraform output -json servers` is the source the Ansible inventory reads.

## 3. Configure the hosts

Both playbooks run against the dynamic inventory, which shells out to
`terraform output`. The operator needs the SSH private key at `~/.ssh/shuttlevn` and
AWS credentials for the same account.

Required operator files, all outside the repositories:

| Path | Content |
| :--- | :--- |
| `~/.ssh/shuttlevn` | SSH private key. |
| `~/.config/shuttlevn/api.env` | API environment, same keys as `.env.example`. |
| `~/.config/shuttlevn/tls/{fullchain,privkey}.pem` | Cloudflare origin certificate. |
| `infrastructure/ansible/inventory/group_vars/all/secrets.yml` | `grafana_admin_password`, `alert_webhook_url`. |

Start from `inventory/group_vars/all/secrets.yml.example`; the real file is
gitignored.

```bash
cd infrastructure/ansible
python3 -m venv ~/.ansible-venv
~/.ansible-venv/bin/pip install ansible-core community.docker

~/.ansible-venv/bin/ansible-playbook playbooks/site.yml
```

`site.yml` imports `common`, `public`, `private`, `exporters`, and `observability`.
The `private` play refuses to continue when a key from `.env.example` is missing or
blank in `api.env`, so a partial environment fails before the container restarts.

### Register the GitHub runners

`runners.yml` is not part of `site.yml`: registration needs a repository-scoped
token that expires in about an hour and is issued by hand. Each host runs a runner
scoped to its own repository, labelled `<inventory_hostname>-runner`.

```bash
~/.ansible-venv/bin/ansible-playbook playbooks/runners.yml \
  -e github_runner_token=<backend-repo-token> --limit private

~/.ansible-venv/bin/ansible-playbook playbooks/runners.yml \
  -e github_runner_token=<frontend-repo-token> --limit public
```

Re-running after registration is a no-op. The runner user joins the `docker` group,
which is root-equivalent on the host; acceptable for single-tenant course hosts,
not for a shared deployment.

## 4. DNS and TLS

Point the Cloudflare `A` record for the site hostname at the public Elastic IP, with
proxying enabled. Both 80 and 443 must stay proxied: the security group accepts
those ports only from Cloudflare ranges, so a direct connection to the IP is
refused by design.

The origin certificate is a Cloudflare Origin CA certificate valid for
`*.murasaki.id.vn`. Cloudflare terminates the public certificate; the edge presents
the origin certificate to Cloudflare only. Origin verification is set to Full
(strict).

## GitHub configuration

Repository variables:

| Name | Value |
| :--- | :--- |
| `AWS_ROLE_ARN` | `terraform_role_arn` output from the bootstrap root. |
| `TF_VAR_PROJECT_NAME` | Same as Terraform `project_name`. |
| `TF_VAR_AMI_ID` | Same as Terraform `ami_id`. |
| `TF_VAR_SSH_ALLOWED_CIDR` | Same as Terraform `ssh_allowed_cidr`. |
| `TF_VAR_ENABLE_NAT_GATEWAY` | `true` / `false`. |

Repository secrets:

| Name | Value |
| :--- | :--- |
| `TF_VAR_SSH_PUBLIC_KEY` | Public key contents. |
| `SSH_PRIVATE_KEY` | Matching private key, for Ansible over SSH. |
| `API_ENV` | Contents of `api.env`. |
| `ANSIBLE_SECRETS` | Contents of `group_vars/all/secrets.yml`. |
| `TLS_FULLCHAIN` | Origin certificate chain. |
| `TLS_PRIVATE_KEY` | Origin certificate key. |
| `SONAR_TOKEN` | SonarQube Cloud token. |
| `SONAR_ORGANIZATION` | SonarQube Cloud organization key. |

Environment `production` with at least one required reviewer. The infrastructure
apply job runs inside it, so no AWS resource changes without approval.

Terraform and Ansible authenticate to AWS with OIDC only. There are no long-lived
access keys in GitHub.

## Pipelines

| Workflow | Trigger | Effect |
| :--- | :--- | :--- |
| `ci-cd.yml` | Push to `main` touching application projects | SonarQube Cloud scan, image to GHCR, deploy on the private runner, health check. |
| `infra.yml` | Push to `main` touching `infrastructure/terraform-aws/**` | `fmt`, `validate`, `plan`. Uploads `tfplan`. Never applies. |
| `infra.yml` | `workflow_dispatch` with `action=apply` | Downloads the plan and applies it after `production` approval. |
| `config.yml` | `workflow_dispatch` | Runs Ansible converge, optionally `--check --diff` or limited to one host group. |

Application deploys never provision infrastructure, and infrastructure changes
never deploy application images.

## Cost control

Two always-on instances plus a NAT gateway bill continuously. When the stack is
idle:

```bash
# Removes the NAT gateway. The private host loses outbound internet, 
# so image pulls and package installs stop working until it returns.
cd infrastructure/terraform-aws
terraform apply -var enable_nat_gateway=false
```

`terraform destroy` removes every resource in the AWS root. It does not touch the
database, the state bucket, the OIDC role, or the DNS record. After a rebuild,
re-point the Cloudflare `A` record at the new Elastic IP.

## Verifying a deployment

```bash
# Edge, through Cloudflare
curl -sI https://<site-hostname>/login

# API, only reachable inside the VPC
ssh ubuntu@<private-ip> 'curl -sf http://127.0.0.1:8080/health'
```

The API does not apply EF Core migrations at startup. Apply them explicitly before
or after a deploy that changes the schema:

```bash
dotnet ef database update --project ShuttleVNBackend.Infrastructure --startup-project ShuttleVNBackend.Api
```

The frontend deploy workflow gates on the `/login` route returning 200; the backend
workflow gates on `/health`.
