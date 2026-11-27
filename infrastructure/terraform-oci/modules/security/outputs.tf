output "public_nsg_id" {
  value = oci_core_network_security_group.public.id
}

output "private_nsg_id" {
  value = oci_core_network_security_group.private.id
}
