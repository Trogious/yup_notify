#!/usr/bin/perl
use strict;

sub sanitize {
  my $file_name = shift;
  rename($file_name, $file_name.'.orig') or die 'no input present: '.$file_name;
  my $input_file;
  my $output_file;
  open($input_file, '<', $file_name.'.orig');
  open($output_file, '>', $file_name);
  while(<$input_file>) {
    s/API_KEY = '.*/API_KEY = 'abc123'/;
    s/ API_KEY = ".*/ API_KEY = "abc123";/;
    s/UPLOADER_SCRIPT =.*/UPLOADER_SCRIPT = '.\/upload.sh'/;
    s/SSL_CERT_PATH =.*/SSL_CERT_PATH = '.\/cert.pem'/;
    s/SSL_KEY_PATH =.*/SSL_KEY_PATH = '.\/key.pem'/;
    s/ ENDPOINT_URI =.*/ ENDPOINT_URI = "https:\/\/127.0.0.1:8000\/notify";/;
    print { $output_file } $_;
  }
  close($input_file);
  close($output_file);
}

sanitize('server.py');
sanitize('yup_notify.cs');
