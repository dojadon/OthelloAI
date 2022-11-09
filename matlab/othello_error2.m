dotnetenv("core");
library = NET.addAssembly('C:\Users\zyand\source\repos\dojadon\OthelloAI\bin\Release\net6.0\OthelloAI.dll');

import OthelloAI.*

n = 1000;
depths = (1:3);

a = zeros(n, 64, length(depths));

for i = 1:n
    game = Tester.TestError(Program.WEIGHT, depths);

    aa = cell2mat(cell(game.Item2));
    result = game.Item1.GetStoneCountGap();
    aa = aa - single(result);

    a(i, :, :) = reshape(aa .* aa, 64, []);
end
%% 

b = squeeze(mean(a, 1));

hold on
for k = 1:length(depths)
    plot(k:(k+43), b(13:56, k));
    % plot(b(13:56, k));
end
hold off

% ylim([400, 1000]);