# Session Manager is the only way in for automation: the hosts accept SSH from
# one operator IP, which a CI runner does not have.
resource "aws_iam_role" "ssm" {
  name = "${var.name}-ssm"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Action    = "sts:AssumeRole"
      Principal = { Service = "ec2.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "ssm" {
  role       = aws_iam_role.ssm.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

resource "aws_iam_instance_profile" "ssm" {
  name = "${var.name}-ssm"
  role = aws_iam_role.ssm.name
}

resource "aws_instance" "this" {
  ami                         = var.ami_id
  instance_type               = var.instance_type
  subnet_id                   = var.subnet_id
  vpc_security_group_ids      = var.security_group_ids
  associate_public_ip_address = var.assign_public_ip
  key_name                    = var.key_name
  iam_instance_profile        = aws_iam_instance_profile.ssm.name

  root_block_device {
    volume_type           = "gp3"
    volume_size           = 30
    encrypted             = true
    delete_on_termination = true
  }

  credit_specification {
    cpu_credits = "standard"
  }

  metadata_options {
    http_endpoint = "enabled"
    http_tokens   = "required"
  }

  tags = {
    Name = var.name
  }

  volume_tags = {
    Name = "${var.name}-root"
  }
}

# Cloudflare points at this address, so it has to survive a replace of the
# instance. Without it every rebuild silently breaks the edge with a 522.
resource "aws_eip" "this" {
  count    = var.assign_public_ip ? 1 : 0
  domain   = "vpc"
  instance = aws_instance.this.id

  tags = {
    Name = "${var.name}-eip"
  }
}