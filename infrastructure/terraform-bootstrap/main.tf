data "aws_caller_identity" "current" {}

locals {
  state_bucket = "shuttlevn-tfstate-${data.aws_caller_identity.current.account_id}"
  oidc_host    = "token.actions.githubusercontent.com"
  repo_owner   = split("/", var.github_repository)[0]
  repo_name    = split("/", var.github_repository)[1]
}

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

# The provider is account-wide and already exists, so it is read here.
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

    # GitHub sub claims now carry immutable numeric ids, e.g.
    # repo:owner@90992374/repo@1320291487:ref:refs/heads/main, so only the
    # id segments are wildcarded.
    condition {
      test     = "StringLike"
      variable = "${local.oidc_host}:sub"
      values = [
        "repo:${local.repo_owner}@*/${local.repo_name}@*:ref:refs/heads/${var.github_branch}",
        "repo:${local.repo_owner}@*/${local.repo_name}@*:environment:${var.github_environment}",
      ]
    }
  }
}

resource "aws_iam_role" "terraform" {
  name               = "shuttlevn-terraform"
  description        = "Assumed by GitHub Actions to plan and apply the ShuttleVN AWS root."
  assume_role_policy = data.aws_iam_policy_document.assume_role.json
}

resource "aws_iam_role_policy_attachment" "terraform_ec2" {
  role       = aws_iam_role.terraform.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonEC2FullAccess"
}

data "aws_iam_policy_document" "state" {
  statement {
    effect    = "Allow"
    actions   = ["s3:ListBucket", "s3:GetBucketLocation", "s3:GetBucketVersioning"]
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
