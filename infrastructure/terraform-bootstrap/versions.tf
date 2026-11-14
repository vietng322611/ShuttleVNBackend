terraform {
  required_version = ">= 1.10.0, < 2.0.0"

  # No backend block on purpose. This root creates the very bucket that the
  # main root stores its state in, so it cannot depend on that bucket. Its
  # state is local and small; it changes roughly never after the first apply.
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}
