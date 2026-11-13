data "aws_caller_identity" "current" {}

locals {
  state_bucket = "shuttlevn-tfstate-${data.aws_caller_identity.current.account_id}"
  oidc_host    = "token.actions.githubusercontent.com"
}

# Remote state for the application root in ../terraform-aws. Versioning is
# what makes a bad apply recoverable: every state write leaves the previous
# one intact.
resource "aws_s3_bucket" "state" {
  bucket = local.state_bucket

  tags = {
    Name = local.state_bucket
  }
}

resource "aws_s3_bucket_versioning" "state" {
  bucket = aws_s3_bucket.state.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "state" {
  bucket = aws_s3_bucket.state.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "state" {
  bucket = aws_s3_bucket.state.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_policy" "state" {
  bucket = aws_s3_bucket.state.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid       = "DenyInsecureTransport"
      Effect    = "Deny"
      Principal = "*"
      Action    = "s3:*"
      Resource = [
        aws_s3_bucket.state.arn,
        "${aws_s3_bucket.state.arn}/*",
      ]
      Condition = {
        Bool = { "aws:SecureTransport" = "false" }
      }
    }]
  })
}

# GitHub-hosted runners have no long-lived keys. They exchange a short-lived
# OIDC token for credentials, and the trust policy pins both the repository
# and the exact refs allowed to do it.
#
# The provider already exists in this account (created by an earlier lab and
# shared account-wide), so it is read, not created. Creating a second one
# with the same URL is rejected by IAM.
data "aws_iam_openid_connect_provider" "github" {
  url = "https://${local.oidc_host}"
}

data "aws_iam_policy_document" "assume_role" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [data.aws_iam_openid_connect_provider.github.arn]
    }

    condition {
      test     = "StringEquals"
      variable = "${local.oidc_host}:aud"
      values   = ["sts.amazonaws.com"]
    }

    # Either a push to the deploy branch (plan) or a job that passed the
    # GitHub Environment approval gate (apply).
    condition {
      test     = "StringLike"
      variable = "${local.oidc_host}:sub"
      values = [
        "repo:${var.github_repository}:ref:refs/heads/${var.github_branch}",
        "repo:${var.github_repository}:environment:${var.github_environment}",
      ]
    }
  }
}

resource "aws_iam_role" "terraform" {
  name               = "shuttlevn-terraform"
  description        = "Assumed by GitHub Actions to plan and apply the ShuttleVN AWS root."
  assume_role_policy = data.aws_iam_policy_document.assume_role.json
}

# The root manages VPCs, subnets, SGs, key pairs, EIPs, NAT gateways and
# EC2 instances: all EC2 API surface, nothing else.
resource "aws_iam_role_policy_attachment" "terraform_ec2" {
  role       = aws_iam_role.terraform.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonEC2FullAccess"
}

# State access is scoped to this bucket only. DeleteObject covers the
# .tflock object that use_lockfile writes and removes.
data "aws_iam_policy_document" "state" {
  statement {
    effect    = "Allow"
    actions   = ["s3:ListBucket", "s3:GetBucketVersioning"]
    resources = [aws_s3_bucket.state.arn]
  }

  statement {
    effect = "Allow"
    actions = [
      "s3:GetObject",
      "s3:GetObjectVersion",
      "s3:PutObject",
      "s3:DeleteObject",
    ]
    resources = ["${aws_s3_bucket.state.arn}/*"]
  }
}

resource "aws_iam_role_policy" "state" {
  name   = "shuttlevn-terraform-state"
  role   = aws_iam_role.terraform.id
  policy = data.aws_iam_policy_document.state.json
}
