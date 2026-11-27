output "network" {
  value = {
    vcn_id            = module.network.vcn_id
    public_subnet_id  = module.network.public_subnet_id
    private_subnet_id = module.network.private_subnet_id
  }
}

output "security" {
  value = {
    public_nsg_id  = module.security.public_nsg_id
    private_nsg_id = module.security.private_nsg_id
  }
}

output "servers" {
  value = {
    public = {
      instance_id = module.public_compute.instance_id
      public_ip   = module.public_compute.public_ip
      private_ip  = module.public_compute.private_ip
    }

    private = {
      instance_id = module.private_compute.instance_id
      private_ip  = module.private_compute.private_ip
    }
  }
}
