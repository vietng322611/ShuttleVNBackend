terraform {
  required_version = ">= 1.10.0, < 2.0.0"

  # Created once by ../../terraform-bootstrap, which cannot itself use this
  # backend (chicken and egg). use_lockfile gives native S3 locking without
  # DynamoDB since Terraform 1.10.
  backend "s3" {
    bucket       = "shuttlevn-tfstate-002944212681"
    key          = "aws/terraform.tfstate"
    region       = "ap-southeast-1"
    encrypt      = true
    use_lockfile = true
  }

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}