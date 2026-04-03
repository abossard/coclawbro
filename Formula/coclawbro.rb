class Coclawbro < Formula
  desc "Claude Code ↔ GitHub Copilot Proxy"
  homepage "https://github.com/abossard/coclawbro"
  license "MIT"
  version "preview"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-osx-arm64.tar.gz"
      sha256 "f661ac3eb754c81af1cf07e002da51b27dc47ff4451131b2ecc986541dddc82b" # osx-arm64
    else
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-osx-x64.tar.gz"
      sha256 "f2c1a3d573043912f36de9b5d252f25a03345be472afed2cce58d9edd1373f34" # osx-x64
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      if Hardware::CPU.is_64_bit?
        url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-arm64.tar.gz"
        sha256 "70d32ffbfd2ab532a0144b1ed702cea9a2b5a81720daa5898d834062ad57dde7" # linux-arm64
      else
        url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-arm.tar.gz"
        sha256 "9abea7ce624f9115a3308c20450862087038fcb114b86417aec2395f383c07d4" # linux-arm
      end
    else
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-x64.tar.gz"
      sha256 "ee916d951e45e3b7f48d1f0eb3368d74f52cfd914206aaa6ed07c38b42187fa8" # linux-x64
    end
  end

  def install
    bin.install "coclawbro"
  end

  test do
    assert_match "coclawbro", shell_output("#{bin}/coclawbro --version")
  end
end
