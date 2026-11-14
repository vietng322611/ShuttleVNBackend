output "state_bucket" {
  value       = aws_s3_bucket.state.bucket
  description = "Put this in the backend block of ../terraform-aws/versions.tf."
}

output "terraform_role_arn" {
  value       = aws_iam_role.terraform.arn
  description = "Set as the AWS_ROLE_ARN repository variable."
}

output "github_oidc_provider_arn" {
  value = data.aws_iam_openid_connect_provider.github.arn
}
