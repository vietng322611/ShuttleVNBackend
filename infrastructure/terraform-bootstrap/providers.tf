provider "aws" {
  region = var.region

  default_tags {
    tags = {
      Project   = "ShuttleVN"
      Course    = "NT548"
      ManagedBy = "Terraform"
    }
  }
}
