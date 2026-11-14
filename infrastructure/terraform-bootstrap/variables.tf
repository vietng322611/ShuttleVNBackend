variable "region" {
  type    = string
  default = "ap-southeast-1"
}

variable "github_repository" {
  type        = string
  description = "owner/repo allowed to assume the deploy role via OIDC."
}

variable "github_branch" {
  type        = string
  description = "Branch whose pushes may assume the deploy role."
  default     = "main"
}

variable "github_environment" {
  type        = string
  description = "GitHub Environment used by the approval-gated apply job."
  default     = "production"
}
